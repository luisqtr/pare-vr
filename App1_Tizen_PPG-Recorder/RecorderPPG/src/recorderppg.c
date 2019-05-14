#include "recorderppg.h"
#include <sensor.h>
#include <time.h>
#include <sap.h>
#include <sap_message_exchange.h>

// NOTE: Added privilege for HealthInfo, and mediastorage
#define LOGFILENAMEPATH "/opt/usr/media/ppg_recorder_logs/"
#define LOGFILENAME "_log.txt"


/*
   Structure to store the data for application logic; it is given
   to each callback invoked through the Application API
*/
typedef struct appdata {
	Evas_Object *win;
	Evas_Object *conform;

	// Add UI elements here
	Evas_Object *label;
	Evas_Object *check;
	Evas_Object *group;
	Evas_Object *sensorBtn;

	bool isRunning;
} appdata_s;
static appdata_s *ad;

struct _sensor_info {
	sensor_type_e sensor_type;
    sensor_h sensor; /* Sensor handle */
    sensor_listener_h sensor_listener; /* Sensor listener */
};
typedef struct _sensor_info sensorinfo_s;

// Variables to handle sensor information and log
static sensorinfo_s sensor_info;
static bool isLogfileOpen = false;
static FILE* logfile;

enum sensors{
	PPG_SIGNAL = 1,
	HR_VALUE = 2,
	RR_SIGNAL = 3,
};

static int sensorOption = PPG_SIGNAL;

// Variables to handle Bluetooth connection
//static bt_error_e ret;
//static bt_adapter_state_e adapter_state;
//static const char* my_uuid="00001101-0000-1000-8000-00805F9B34FB";		// Randomly generated UUID, must be the same in sender app and receiver app??
//static int server_socket_fd = -1;

static bool isBluetoothConnected = false;

static void
win_delete_request_cb(void *data, Evas_Object *obj, void *event_info)
{
	ui_app_exit();
}

static void
win_back_cb(void *data, Evas_Object *obj, void *event_info)
{
	appdata_s *ad = data;
	/* Let window go to hide state. */
	elm_win_lower(ad->win);
}

//////////////////////////////////
/////////////// LOG FILES MANAGEMENT
//////////////////////////////////
int
get_timestamp_string(char* timestr) //, size_t buff_len)
{
    time_t t = time(NULL);
    struct tm tm = *localtime(&t);
    sprintf(timestr,"%d%02d%02d%02d%02d%02d_", tm.tm_year + 1900, tm.tm_mon + 1, tm.tm_mday, tm.tm_hour, tm.tm_min, tm.tm_sec);
    return 0;
}

static void
append_log_data(const char* logLine)
{
	if(isLogfileOpen)
	{
		fprintf(logfile, "%s", logLine);
	}
}

static void
create_new_log_file(const char* filePrefix)
{
	// Create folder if it does not exist
	struct stat st = {0};
	if (stat(LOGFILENAMEPATH, &st) == -1) {
	    mkdir(LOGFILENAMEPATH, 0700);
	}

	char filepath[PATH_MAX];
	char timestamp[30];

	get_timestamp_string(timestamp);

	strcpy(filepath, LOGFILENAMEPATH);
	strcat(filepath, timestamp);
	strcat(filepath, filePrefix);
	strcat(filepath, LOGFILENAME);

	logfile = fopen(filepath, "a");

	isLogfileOpen = true;

	// Log file headers
	append_log_data("type,timestamp_usec,value,accuracy\r\n");
}

static void
close_current_log_file()
{
	isLogfileOpen = false;
	fclose(logfile);
}

//////////////////////////////////
/////////////// COMMUNICATION Samsung Accessory Protocol (SAP)
//////////////////////////////////

// Control if the application has a peer connected to send data through bluetooth
void change_bluetooth_state(bool state)
{
	isBluetoothConnected = state;
}


//////////////////////////////////
/////////////// SENSOR CALLBACKS
//////////////////////////////////

static void
on_sensor_event(sensor_h sensor, sensor_event_s *event, void *user_data)
{
    // Select a specific sensor with a sensor handle
    // This example uses sensor type, assuming there is only 1 sensor for each type
	/*typedef struct
	{
	   int accuracy;
	   unsigned long long timestamp;
	   int value_count;
	   float values[MAX_VALUE_SIZE];
	}
	sensor_event_s;

	accuracy:
	timestamp: microseconds
	values[0]: 	 HRM green light value - int - minValue=0|maxValue=1081216 (maybe:1065353216)
			 	 HRM - int - minValue=0|maxValue=240
	*/

	if(sensor_info.sensor != sensor)
	{
		dlog_print(DLOG_DEBUG, LOG_TAG, "Sensor callback from a different listener.");
		return;
	}

	sensor_type_e type;
	sensor_get_type(sensor, &type);

	// Retrieve data
	char log_line[128];	// [PATH_MAX]
	unsigned long long timestamp_microsec = event->timestamp;
	int accuracy = event->accuracy;
	float value = 0;

	if(type == SENSOR_HRM_LED_GREEN)
	{
		value = event->values[0];

		sprintf(log_line, "PPG,%llu,%.0f,%i\r\n", timestamp_microsec, value, accuracy);

		// Write in the logfile that is currently active
		append_log_data(log_line);

		// Use sensor information
		dlog_print(DLOG_DEBUG, LOG_TAG, "SENSOR_HRM_LED_GREEN data: %.2f", value);
	}
	else if(type == SENSOR_HRM)
	{
		// Retrieve data
		value = event->values[0];

		sprintf(log_line, "HR,%llu,%.2f,%i\r\n", timestamp_microsec, value, accuracy);

		// Write in the logfile that is currently active
		append_log_data(log_line);

		// Use sensor information
		dlog_print(DLOG_DEBUG, LOG_TAG, "SENSOR_HRM data: %.2f", value);
	}
	else
	{
		dlog_print(DLOG_ERROR, LOG_TAG, "Event without receiver");
	}



	// Send via bluetooth with timestamp
	if(isBluetoothConnected)
	{
		// Send through SAP
		send_message(log_line, (int)strlen(log_line));

		dlog_print(DLOG_DEBUG, LOG_TAG, "Sent message:%s", log_line);
	}
}

static void
sensor_start(appdata_s* ad, sensor_type_e sensor_type, unsigned int update_interval_millisec)
{
	// Initialize sensor
	sensor_info.sensor_type = sensor_type;
	int error;

	//// Check if is supported
	bool is_supported = false;
	char buf[PATH_MAX];
	error = sensor_is_supported(sensor_info.sensor_type, &is_supported);
	sprintf(buf, "Sensor is %s", is_supported ? "supported" : "not supported");
	dlog_print(DLOG_INFO, LOG_TAG,buf);

	if(is_supported)
	{
		//// Request sensor and setup callback
		error = sensor_get_default_sensor(sensor_info.sensor_type, &(sensor_info.sensor));
		dlog_print(DLOG_INFO, LOG_TAG,"Get default sensor: %d", error);

		error = sensor_create_listener(sensor_info.sensor, &(sensor_info.sensor_listener));
		dlog_print(DLOG_INFO, LOG_TAG,"Create listener: %d", error);

		/*
		//// Sensor Info
		sensor_type_e type;
		float min_range;
		float max_range;
		float resolution;
		int min_interval;

		error = sensor_get_type(sensor_info.sensor_listener, &type);
		error = sensor_get_min_range(sensor_info.sensor_listener, &min_range);
		error = sensor_get_max_range(sensor_info.sensor_listener, &max_range);
		error = sensor_get_resolution(sensor_info.sensor_listener, &resolution);
		error = sensor_get_min_interval(sensor_info.sensor_listener, &min_interval);

		sprintf(buf, "Sensor details | type:%d, min:%f, max:%f, resol:%f, minInt:%d",type,min_range,max_range,resolution,min_interval);
		dlog_print(DLOG_INFO, LOG_TAG,buf);
		*/

		// Setup listener
		error = sensor_listener_set_event_cb(sensor_info.sensor_listener, update_interval_millisec, on_sensor_event, NULL);
		dlog_print(DLOG_INFO, LOG_TAG,"Set sensor cb: %d", error);

		sensor_listener_set_option(sensor_info.sensor_listener, SENSOR_OPTION_ALWAYS_ON);

		error = sensor_listener_start(sensor_info.sensor_listener);
		dlog_print(DLOG_INFO, LOG_TAG,"Starting sensor listener: %d", error);
	}
}

static void
sensor_stop(appdata_s* ad)
{
	int error;
	sensor_listener_set_option(sensor_info.sensor_listener,SENSOR_OPTION_DEFAULT);

	error = sensor_listener_stop(sensor_info.sensor_listener);
	dlog_print(DLOG_INFO, LOG_TAG,"Stopping sensor listener: %d", error);

	error = sensor_destroy_listener(sensor_info.sensor_listener);
	dlog_print(DLOG_INFO, LOG_TAG,"Destroying sensor listener: %d", error);

	sensor_info.sensor = NULL;
	sensor_info.sensor_type = SENSOR_ALL;
	sensor_info.sensor_listener = NULL;
}


//////////////////////////////////
/////////////// USER INTERFACE CALLBACKS
//////////////////////////////////

// TOAST
static void
_timeout_cb(void *data, Evas_Object *obj, void *event_info)
{
	if (!obj) return;
	elm_popup_dismiss(obj);
}

static void
_block_clicked_cb(void *data, Evas_Object *obj, void *event_info)
{
	if (!obj) return;
	elm_popup_dismiss(obj);
}

static void
_popup_hide_cb(void *data, Evas_Object *obj, void *event_info)
{
	if (!obj) return;
	elm_popup_dismiss(obj);
}

static void
_popup_hide_finished_cb(void *data, Evas_Object *obj, void *event_info)
{
	if (!obj) return;
	evas_object_del(obj);
}

static void
_popup_toast_cb(Evas_Object *parent, char *string)
{
	Evas_Object *popup;

	popup = elm_popup_add(parent);
	elm_object_style_set(popup, "toast/circle");
	elm_popup_orient_set(popup, ELM_POPUP_ORIENT_BOTTOM);
	evas_object_size_hint_weight_set(popup, EVAS_HINT_EXPAND, EVAS_HINT_EXPAND);
	eext_object_event_callback_add(popup, EEXT_CALLBACK_BACK, _popup_hide_cb, NULL);
	evas_object_smart_callback_add(popup, "dismissed", _popup_hide_finished_cb, NULL);
	elm_object_part_text_set(popup, "elm.text", string);

	evas_object_smart_callback_add(popup, "block,clicked", _block_clicked_cb, NULL);

	elm_popup_timeout_set(popup, 2.0);

	evas_object_smart_callback_add(popup, "timeout", _timeout_cb, NULL);

	evas_object_show(popup);
}

void
toast_message(char *data)
{
	dlog_print(DLOG_INFO, LOG_TAG, "Updating UI with data %s", data);
	_popup_toast_cb(ad->win, data);
}

// Start/Stop callback
static void
start_stop_button_click_cb(void *data, Evas_Object *button, void *ev)
{
	appdata_s *ad = data;

	if(!ad->isRunning)
	{
		if(sensorOption == PPG_SIGNAL)
		{
			// Start LOG
			create_new_log_file("ppg");
			// communication_start(); // Bluetooth SPP RFCOMM
			sensor_start(ad, SENSOR_HRM_LED_GREEN, 20);
		}
		else if(sensorOption == HR_VALUE)
		{
			create_new_log_file("hr");
			sensor_start(ad, SENSOR_HRM, 500);
		}

		dlog_print(DLOG_INFO, LOG_TAG, "Start button clicked\n");

		elm_object_text_set(ad->sensorBtn, "STOP SENSOR");
		ad->isRunning = true;
	}
	else
	{
		// Stop process
		sensor_stop(ad);
		// communication_stop(); // Bluetooth SPP RFCOMM
		close_current_log_file();

		elm_object_text_set(ad->sensorBtn, "START SENSOR");
		elm_object_text_set(ad->label, "<align=center>HRM Recorder</align>");

		dlog_print(DLOG_INFO, LOG_TAG, "Stop button clicked\n");
		ad->isRunning = false;
	}
}

// Radio Callback
void
changed_cb(void *data, Evas_Object *obj, void *event_info)
{

	if(elm_check_state_get(ad->check) == EINA_TRUE)
	{
		toast_message("Variable to record:<br> HR at 2Hz");
		sensorOption = HR_VALUE;
	}
	else
	{
		toast_message("Variable to record:<br> PPG at 50Hz");
		sensorOption = PPG_SIGNAL;
	}

    dlog_print(DLOG_INFO, LOG_TAG, "The value of the check has changed: %i\n", sensorOption);
}


//////////////////////////////////
/////////////// CREATES THE USER INTERFACE
//////////////////////////////////

// Helper to build the UI within a BOX element
static void
my_box_pack(Evas_Object *box, Evas_Object *child,
            double h_weight, double v_weight, double h_align, double v_align)
{
    /* Tell the child packed into the box to be able to expand */
    evas_object_size_hint_weight_set(child, h_weight, v_weight);
    /* Fill the expanded area (above) as opposed to centering in it */
    evas_object_size_hint_align_set(child, h_align, v_align);
    /* Set the child as the box content and show it */
    evas_object_show(child);
    elm_object_content_set(box, child);

    /* Put the child into the box */
    elm_box_pack_end(box, child);
    /* Show the box */
    evas_object_show(box);
}

static void
create_base_gui(appdata_s *ad)
{
	/* Window */
	/* Create and initialize elm_win.
	   elm_win is mandatory to manipulate window. */
	ad->win = elm_win_util_standard_add(PACKAGE, PACKAGE);
	elm_win_autodel_set(ad->win, EINA_TRUE);

	if (elm_win_wm_rotation_supported_get(ad->win)) {
		int rots[4] = { 0, 90, 180, 270 };
		elm_win_wm_rotation_available_rotations_set(ad->win, (const int *)(&rots), 4);
	}

	evas_object_smart_callback_add(ad->win, "delete,request", win_delete_request_cb, NULL);
	eext_object_event_callback_add(ad->win, EEXT_CALLBACK_BACK, win_back_cb, ad);

	/* Conformant */
	/* Create and initialize elm_conformant.
		   elm_conformant is mandatory for base gui to have proper size
		   when indicator or virtual keypad is visible. */
	ad->conform = elm_conformant_add(ad->win);
	elm_win_indicator_mode_set(ad->win, ELM_WIN_INDICATOR_SHOW);
	elm_win_indicator_opacity_set(ad->win, ELM_WIN_INDICATOR_OPAQUE);
	evas_object_size_hint_weight_set(ad->conform, EVAS_HINT_EXPAND, EVAS_HINT_EXPAND);
	elm_win_resize_object_add(ad->win, ad->conform);
	evas_object_show(ad->conform);


	/* Box can contain other elements in a vertical line (by default) */
	Evas_Object *box = elm_box_add(ad->win);
	evas_object_size_hint_weight_set(box, EVAS_HINT_EXPAND, EVAS_HINT_EXPAND);
	evas_object_size_hint_align_set(box, EVAS_HINT_EXPAND, EVAS_HINT_EXPAND);
	elm_object_content_set(ad->conform, box);
	evas_object_show(box);


	/* Label */
	/* Create an actual view of the base gui.
		   Modify this part to change the view. */
	ad->isRunning = false;

	ad->label = elm_label_add(ad->conform);
	elm_object_text_set(ad->label, "<align=center>HRM Recorder</align>");
	my_box_pack(box, ad->label, 1.0, 0.0, -1.0, -1.0);
	/*
		evas_object_size_hint_weight_set(ad->label, EVAS_HINT_EXPAND, EVAS_HINT_EXPAND);
		elm_object_content_set(ad->conform, ad->label);
	 */

	/* Start button*/
	ad->sensorBtn = elm_button_add(ad->conform);
	elm_object_text_set(ad->sensorBtn,"START SENSOR");
	//elm_object_content_set(ad->conform, ad->sensorBtn); // Add an object to the conformant (Canvas)
	my_box_pack(box, ad->sensorBtn, 1.0, 0.0, -1.0, -1.0);
	// Callbacks
	evas_object_smart_callback_add(ad->sensorBtn, "clicked", start_stop_button_click_cb, ad);

	/* Add a box to pack a check */
	ad->check = elm_check_add(box);
	elm_object_style_set(ad->check, "default");
	evas_object_show(ad->check);
	elm_box_pack_end(box, ad->check);
	evas_object_smart_callback_add(ad->check, "changed", changed_cb, NULL);

	/* Show window after base gui is set up */
	evas_object_show(ad->win);
}

//////////////////////////////////
/////////////// APPLICATION LIFE-CYCLE CALLBACKS
//////////////////////////////////

static bool
app_create(void *data)
{
	/* Hook to take necessary actions before main event loop starts
		Initialize UI resources and application's data
		If this function returns true, the main loop of application starts
		If this function returns false, the application is terminated */
	ad = data;

	create_base_gui(ad);

	initialize_sap(ad);

	return true;
}

static void
app_control(app_control_h app_control, void *data)
{
	/* Handle the launch request. */
}

static void
app_pause(void *data)
{
	/* Take necessary actions when application becomes invisible. */
}

static void
app_resume(void *data)
{
	/* Take necessary actions when application becomes visible. */
}

static void
app_terminate(void *data)
{
	/* Release all resources. */
}


//////////////////////////////////
/////////////// SYSTEM-RELATED EVENTS CALLBACKS
//////////////////////////////////

static void
ui_app_lang_changed(app_event_info_h event_info, void *user_data)
{
	/*APP_EVENT_LANGUAGE_CHANGED*/
	char *locale = NULL;
	system_settings_get_value_string(SYSTEM_SETTINGS_KEY_LOCALE_LANGUAGE, &locale);
	elm_language_set(locale);
	free(locale);
	return;
}

static void
ui_app_orient_changed(app_event_info_h event_info, void *user_data)
{
	/*APP_EVENT_DEVICE_ORIENTATION_CHANGED*/
	return;
}

static void
ui_app_region_changed(app_event_info_h event_info, void *user_data)
{
	/*APP_EVENT_REGION_FORMAT_CHANGED*/
}

static void
ui_app_low_battery(app_event_info_h event_info, void *user_data)
{
	/*APP_EVENT_LOW_BATTERY*/
}

static void
ui_app_low_memory(app_event_info_h event_info, void *user_data)
{
	/*APP_EVENT_LOW_MEMORY*/
}

int
main(int argc, char *argv[])
{
	appdata_s ad = {0,};
	int ret = 0;

	ui_app_lifecycle_callback_s event_callback = {0,};
	app_event_handler_h handlers[5] = {NULL, };

	event_callback.create = app_create;
	event_callback.terminate = app_terminate;
	event_callback.pause = app_pause;
	event_callback.resume = app_resume;
	event_callback.app_control = app_control;

	ui_app_add_event_handler(&handlers[APP_EVENT_LOW_BATTERY], APP_EVENT_LOW_BATTERY, ui_app_low_battery, &ad);
	ui_app_add_event_handler(&handlers[APP_EVENT_LOW_MEMORY], APP_EVENT_LOW_MEMORY, ui_app_low_memory, &ad);
	ui_app_add_event_handler(&handlers[APP_EVENT_DEVICE_ORIENTATION_CHANGED], APP_EVENT_DEVICE_ORIENTATION_CHANGED, ui_app_orient_changed, &ad);
	ui_app_add_event_handler(&handlers[APP_EVENT_LANGUAGE_CHANGED], APP_EVENT_LANGUAGE_CHANGED, ui_app_lang_changed, &ad);
	ui_app_add_event_handler(&handlers[APP_EVENT_REGION_FORMAT_CHANGED], APP_EVENT_REGION_FORMAT_CHANGED, ui_app_region_changed, &ad);
	ui_app_remove_event_handler(handlers[APP_EVENT_LOW_MEMORY]);

	ret = ui_app_main(argc, argv, &event_callback, &ad);
	if (ret != APP_ERROR_NONE) {
		dlog_print(DLOG_ERROR, LOG_TAG, "app_main() is failed. err = %d", ret);
	}

	dlog_print(DLOG_INFO, LOG_TAG, "App started");

	return ret;
}
