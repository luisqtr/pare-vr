#ifndef __recorderppg_H__
#define __recorderppg_H__

#include <app.h>
#include <Elementary.h>
#include <system_settings.h>
#include <efl_extension.h>
#include <dlog.h>

#ifdef  LOG_TAG
#undef  LOG_TAG
#endif
#define LOG_TAG "recorderppg"


void initialize_sap();
void send_message(char* message, int length);

void change_bluetooth_state(bool state);
void toast_message(char *data);

#if !defined(PACKAGE)
#define PACKAGE "com.mimerse.ppgmonitor"
#endif

#endif /* __recorderppg_H__ */
