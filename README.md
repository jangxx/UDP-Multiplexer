# ![Logo](/resources/icon_readme.png) UDP Multiplexer ![Logo](/resources/icon_readme.png)

A small utility that receives UDP packets on one or more ports and forwards them to one or more addresses.

## Installation

There is no installation to speak of.
Simply head over to the [Release](https://github.com/jangxx/UDP-Multiplexer/releases/latest) section and download the latest version.
Unzip the file and run the enclosed executable.

## Usage

When launching the app you are presented with this window:

![screenshot](/resources/screenshot_1.PNG)

The _Inputs_ and _Outputs_ sections function identically.
You simply enter an address and a port to bind or send to respectively.
Clicking the add button adds another row to the specific section and clicking on the _X_ removes the entry.

After the addresses are entered, clicking on _Start_ will begin the packet forwarding.

### Configuration file

The _File_ menu contains two buttons to save and load the current config in a config file.
You can either load the config file through the menu or supply it as the first launch parameter, e.g. by setting up a shortcut or by dropping the config file on the executable or a shortcut to it (basically to "open" the config file with the app).

### Settings
**Start when launched from config file**:  
When this is checked the packet forwarding automatically starts when a config file in opened with the app.
This allows you to set up a shortcut to the app which, when opened, automatically loads the config and starts the packet forwarding all with a single click.