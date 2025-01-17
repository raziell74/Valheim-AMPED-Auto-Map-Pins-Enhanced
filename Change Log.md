# Change Log

### Version v1.3.5
  * Performance Overhaul! Updated and refactored a large portion of the mod to run faster as to not cause as much FPS drops for potato computers. Lots of legacy code updated.
  * Updated and reorganized configuration. My sincerest apologies but this will require you to redo your configuration settings, my first go at it a lot of the naming is bad and would cause issues and I now know a ton more about the inner workings of BepInEx configurations. If you experience issues, delete your `amped.mod.auto_map_pins.cfg` and restart the game to generate a clean config. Thank you for your patience, this is my first time doing any BepInEx modding.
  * Mod will now properly refresh pins if making changes to configuration values while in game. No more need to restart the game for the new values to take effect!
  * Added new config value for "Minimap Pin Size Multiplier". The mod will multiply the pin size by this multiplier for the minimap so that players can better control the pin size in the minimap
  * Added minimapSize for Pin Type configuration. JSON configs now support a new optional value "minimapSize" which allows you to custom set the minimap Pin Size. If set the new Minimap Pin Size Multiplier config will use this minimapSize value to multiply the pin size in the minimap so you can better differentiate important pins
  * Combined all of the object id logger configs into a single "objectLogging" config value. This new config will toggle all object id logs
  * Added Object Id filter for object logging. This is a comma delimited list of object ids you don't want the mod to report when it encounters them. This helps clean up the logs and only shows you the object ids you want
  * Added "Unique Objects Only". If enabled the mod will only report an object id once when it first encounters it instead of for every object. When the game exists it will log out the full list of unique object ids that were discovered during the session
  * Removed a lot of broken code around Commands. Using the Configuration Manager almost all the previous "Commands" are possible without having to remember chat commands (that didn't even work)
  * pinRange now is defaulted to using the Players Explore Radius. This can be disabled for a custom pinRange and will be compatible with any mods that change the explore radius dynamically like the Cartography skill or mods that increase the explore radius while on boats. The pinning range will update dynamically as the radius changes
  * BugFix - Oak Trees are now correctly detected, Thanks to LuxZg for helping out with this one
  * BugFix - Fixed the issue where Saved Pins would get additional pins overlaid on top of them when you save and reload the game near one. Now you can see if you've checked (x'ed out) a pin or not and it will persist until the pin is removed
  * BugFix - Manually removing pins from the map is easier now, increased pin detection radius when clicking to remove a pin from the map
  * BugFix - Corrected Draugr and Skeleton spawner object ids

### Version v1.3.4
  * New config for hiding pin types by label has been added. Users can now entered a comma seperated list of pin labels that they don't want to be autopinned
  * Multiple pin type configuration json files can now be loaded. Opening up the possibility of other authors to release their own pin and icon packs
  * Pin Config file has been made dynamic. AMPED will now load any json file with the prefix 'amp_' and attempt to use it as a pin type config file
  * Overhauled sprite loading to instead search the plugins folder and use the first file that matches the file name
  * Refactored some internal code to be more maintainable

### Version v1.3.3
  * Added new pins
  * New Pin: Infested Tree (Guck Trees)
  * New Pin: Road Post
  * New Pin: Dvergr Tower
  * New Pin: Statues
  * New Pin: Ruins
  * New Pin: Stone Tower
  * New Pin: Runestone
  * New Pin: Giant Remains (petrified remains and soft tissue)
  * New Pin: Stone Tower
  * New Pin: Log Cabin
  * New Pin: Infested Mine
  * New Pin: Dvergr Exavation
  * New Pin: Swamp Hut
  * New Pin: Swamp Tower
  * New Pin: Giant Armor (Giant sword/helmets)
  * New Pin: Wood House
  * New Pin: Stone Circle
  * New Pin: Well
  * New Pin: Dolmen
  * New Pin: Fuling Tower
  * New Pin: Harbour
  * New Pin: Viaduct
  * New Pin: Stone Tower
  * New Pin: Shipwreck
  * New Pin: Farm House

### Version v1.3.1/1.3.2
  * Fixed an error with r2modmanager not finding assets

### Version v1.3.0
  * Overhauled configuration of map pin types 
  * Speed optimizations for loading in pins
  * Ability to toggle logging. Users can now enable the logging to print out object id's that come within range of the player so that players can add custom map pins to the new amp_pin_types.json file and pin what ever they would like, even provide their own custom icon for it. 
  * New Pins added: Mountain Caves, Tar Pits, Fuling Camps, Jotun Puffs, Mage Caps, and Black Cores
  