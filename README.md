# PARE-VR
## Physiologically Adaptable Relaxation Experience through Virtual Reality

Project for Master Thesis in Health Informatics

Karolinska Institutet & Stockholm University, Sweden

**By: Luis Quintero** | https://luiseduve.github.io

## Description
Project that provides VR applications developed in Unity with access to HR and PPG data to execute physiologically adaptable behaviors.

The architecture consists on three application as shown in following image.

![Technical Architecture](https://github.com/luiseduve/pare-vr/blob/master/docs/img/TechnicalArchitecture.png)

The first application is developed in C using Tizen Studio and can be found in [App 1](App1_Tizen_PPG-Recorder/). It collects HR and PPG data from Samsung Smartwatches and send it through SAP (Samsung Accessory Protocol) to the paired mobile phone.

The second application is developed in Java using Android Studio and can be found in [App 2](App2_PhysioSense_v2/). Based on the framework ![PhysioVR](https://github.com/PhysioTools/PhysioVR). It receives data from smartwatches through SAP protocol and runs an algorithm to detect peaks and calculate HRV from the collected signal. Then, the results of the calculation are sent through UDP to a mobile VR application to perform adaptations based on physiological data.

The last application is a package developed in Unity contains the scripts to receive UDP data and design adaptation rules based. The provided files are not complete because the designed test used the proprietary software ![CalmPlace](https://mimerse.com/products/calm-place/). The files can be adapted for any Unity application deployed on Android.

To see instructions to setup the environments open the file [BriefDevGuide](docs/Brief-DevGuide-PARE-VR.pdf)

To see the whole thesis visit ![Master Thesis](http://luiseduve.github.io/files/2019_MasterThesis_LuisQuintero.pdf)