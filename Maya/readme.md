Maya to Babylon.js exporter
==============================

Documentation: http://doc.babylonjs.com/resources/maya

# How to contribute:
## Requirements:
* Install Visual Studio (community editon works)
* Install Maya.

## Develop:
Use "RaiseMessage/Warning" methods to see if your code is working
* Close all running Maya instances.
* Build project with Admin-elevated instance of Visual Studio.
* Maya should be relaunched by the build system, run the exporter, the messages will be displayed in the exporter form.

## Debug:
Ensure that msbuild has been added to the system path for all users
* Open a command shell as administrator and navigate to the project root
* Build Maya2Babylon using the following command:
    cicd\batch\buildMaya2Babylon.bat [MAYA_VERSION]
* Test Maya2Babylon using the following command:
    cicd\batch\testMaya2Babylon.bat [MAYA_VERSION]
