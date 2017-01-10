# CameraSpy - Overview
Interactive map to allow viewing of unsecured cameras in the C4 list.  I wrote this at 3AM because I couldn't sleep that night, but it turned out to be useful for both killing boredom and providing assurance of the security of your own setup.

## Video Demo
You can find a short video demo here: https://youtu.be/HRD_svoX_0k.  The first few seconds are the application trying to render 1GB of objects on Bing Maps, so bear with it.

## Attribution
None of this would be possible without the efforts of [minxomat](https://github.com/turbo).  Check out the link to see more quality work.

This application leverages the IP6.LITE CSV available from [lite.ip2location.com](http://lite.ip2location.com).

## Installation

### Installation Package
*Coming soon!*

### From Source
#### Dependencies
* You'll need to install Visual Studio, .NET 4.6.2, and a copy of the [Bing Maps SDK for Windows](https://marketplace.visualstudio.com/items?itemName=Microsoft-BingMapsTeam.BingMapsSDKforWindows81Storeapps).
* Acquire a copy of [minxomat's](https://github.com/turbo) [C4 list](https://git.io/c4). Save it to a file named "c4" with no extension
* Download a copy of the [ip2location DB6 Lite CSV for IPv4](http://lite.ip2location.com/database/ip-country-region-city-latitude-longitude-zipcode).  You may need to make an account with them to download.

#### Compilation

*Where ~ denotes the directory you've extracted or cloned this repository to*

1. Open the ~/CameraSpy.sln document in Visual Studio
2. Select "Build -> Build Solution"
3. A debug binary will be compiled into ~/CameraSpy/bin/Debug/

#### Execution

1. Create a folder named "data" alongside the compiled exe.
2. Copy the C4 dependency into the "data" folder
3. Copy the DB6 dependency into "data," and rename this file to "IP_DB6" with no extension
4. Double click CameraSpy.exe and go nuts!
