# MSFS Pop Out Panel Manager 2024

MSFS Pop Out Panel Manager 2024 is an application for Microsoft Flight Simulator 2024. It helps pop out, save and position pop out panels to be used by utilities such as Sim Innovations Air Manager or to place pop out panels onto your screen or another monitor at predetermined locations automatically. It also adds touch capability to pop out panels that are operated on touch screen which is currently not supported by the game.

## Note: (Still under investigation/development) POPM functionality that does not work with MSFS 2024
- Auto Pop Out 
  * My community plugin "ready-to-fly-button-skipper" to skip the "Ready to Fly" screen no longer works. You will need to manually click "Ready to Fly" to initiate auto pop out. You can set the auto pop out delay in preference setting depending on the speed of your computer.
  * Worked around instruction has been created if you're comfortable modifying MSFS game file.
  * Auto Pop Out can still be triggered but you need to manually click the "Ready to Fly" button during aircraft loading cut scene.

## Getting Started

[Please see Getting Started guide to setup your first profile](./GETTING_STARTED.md)

Video showing how to create a new aircraft profile and panel source selection:</br> 
https://vimeo.com/917361559

Video showing how to use the new floating panel feature:</br> 
https://vimeo.com/918153200

<br>

## How to Install 

1. After downloading the latest zip package from github repository or from Flightsim.to website, extract the zip package to a folder of your choice on your computer.

2. Start the application **MSFS Pop Out Panel Manager 2024.exe** and it will automatically connect when MSFS/SimConnect starts. 

4. If Pop Out Panel Manager is not connecting to the game (green connection icon in the upper left corner of the app), you may be missing VC++ redistributable that is required for SimConnect to work. Please download and install the following [VC++ redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe) on your PC to resolve this issue. 

<br>

## How to Update

1. To update the application, you can download the latest zip package and directly extract the package into your Pop Out Manager installation folder and overwrite all files within. Your application setting and profile data will be safe.

2. You can also use the built-in auto update feature and let the application handles the update. If the update is optional, you can skip the update if you so choose. When you start the application and if an update is available, a dialog will appear and it will show the latest version's release notes and an option to update the application.

3. If you're not being prompt for update when new update is available, please try the following fix:

  - Restart you computer and most of the time this will do the trick.
  - Clear your default web browser cache on your computer since auto update will try to download latest version of update configuration file from github repository and the file may have been cached on your machine.
  - Clear Internet Browser History.  First search for "Internet Options" in Windows control panel. In "General" tab, select "Delete" in Browsing History section.

<br>


## Application Features
* Display resolution independent. Supports 1080p/1440p/4k display and ultrawide displays.

* Support multiple user defined aircraft profiles to save panel locations to be recalled later.

* Intuitive user interface to defined location of source panels to be popped out and configure the size and location of pop out panels.

* Enable touch support for pop outs on touch capable display.

* Auto game refocus to enable operation of pop out panels either by click or touch without losing flight control.

* Auto Pop Out feature. The application will detect active aircraft and will activate the corresponding profile on start of new flight session (after manually clicking ready to fly button).

* Fine-grain control of positioning of panels down to pixel level. 

* Panels can be configured to appear always on top, with title bar hidden, or stretch to full screen mode.

* Auto disable Track IR when pop out starts.

* User-friendly features such as application always on top, auto start, minimized to tray with keyboard shortcuts.

* Auto save feature. All profile and panel changes are saved automatically.

<br>

## User Profile Data Files

The user plane profile data and application settings data are stored as JSON files. The path for the folder can be configure under Preferences -> General Settings -> Folder Path to Store POPM Configuration and Profiles.

## Author
Stanley Kwok
[hawkeyesk@outlook.com](mailto:hawkeyesk@outlook.com) 

I welcome feedback to help improve the usefulness of this application. You are welcome to take a copy of this code to further enhance it and use within your own project. But please abide by licensing terms and keep it open source:)

## Donation

[<img src="https://www.paypalobjects.com/en_US/i/btn/btn_donate_LG.gif"
     alt="Markdown Monster icon"
     style="float: left; margin-right: 10px;" />](https://www.paypal.com/donate/?business=NBJ7SZR7MUDE6&no_recurring=0&item_name=Thank+you+for+your+kind+support+of+MSFS+Pop+Out+Panel+Manager%21&currency_code=USD)

Thank you for your super kind support of this app!