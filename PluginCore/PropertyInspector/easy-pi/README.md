# StreamDeck EasyPI-v2

This library is meant to simplify the communication between the Property Inspector and your plugin.
By sticking to a few guidelines, the library will own setting and getting the various settings for your plugin.

## Help / Support
Make sure to star this repository or follow my [twitter](https://twitter.com/realBarRaider) to get notified when it changes.  
**Questions, Suggestions, and Support via [Discord](http://discord.barraider.com)**

## New in v1.4
- Updated styles  to match Elgato's

## New in v1.3
- Introducing rangedTooltip.js - Instructions how to use it in the ***Ranged Tooltip*** section below

## How to use:
Use the following tags in your Property Inspector HTML file to use the latest version:
 - Core (sdtools.common.js):
```
<script src="https://cdn.jsdelivr.net/gh/barraider/streamdeck-easypi-v2@latest/src/sdtools.common.js"></script>
```  
 - Core CSS (sdpi.css): 
 ```
<link rel="stylesheet" href="https://cdn.jsdelivr.net/gh/barraider/streamdeck-easypi-v2@latest/src/sdpi.css">
 ```  
 - Range Tooltips (see usage examples below):
```
<link rel="stylesheet" href="https://cdn.jsdelivr.net/gh/barraider/streamdeck-easypi-v2@latest/src/rangeTooltip.css">
<script src="https://cdn.jsdelivr.net/gh/barraider/streamdeck-easypi-v2@latest/src/rangeTooltip.js"></script>
``` 

## Tutorial
### Common Use-case
This section is relevant for the majority of controls. There are a few different controls (such as the CheckBox or FilePicker) that require additional steps, as indicated below.   
Example1:  
```
<input class="sdProperty" id="lastName" oninput="setSettings()">
```

Example2:  
```
 <select class="sdProperty" id="refreshSeconds" oninput="setSettings()">
	<option value="1">1 Second</option>
	<option value="15">15 Seconds</option>
	<option value="30">30 Seconds</option>
</select>

```

There are three things needed for the library to work correctly (as demonstrated in the example above):
1. Each property must have an ***id*** field. The id must be identical to the name of the property in the payload. In the example above, the library will pass two settings, one named ***lastName*** and one named ***refreshSeconds***. The library will also look for those names to populate the values when new data is received.

2. The property must also have a class called ***sdProperty***. The control can have additional classes too, but this one must be there.

3. Add an ***oninput="setSettings()"*** section to the control, to ensure settings are saved as soon as a user changes input.

### CheckBox
To get data to and from checkboxes add an additional class named **sdCheckbox** _in addition_ to the sdProperty indicated above.

```
<input id="enterMode" type="checkbox" class="sdProperty sdCheckbox" oninput="setSettings()">
```

### FilePicker
To support filepickers, as recommended in the SDK follow the following guidelines:

```
<input class="sdProperty sdFile" type="file" id="userImage" oninput="setSettings()">
<label class="sdpi-file-info " for="userImage1" id="userImageFilename">No file...</label>
```

1. On the input element, add an additional class named **sdFile** _in addition_ to the sdProperty indicated above.
2. Add a label, as indicated above. Make sure the Id of the label has a ***Filename*** suffix (If the input is called userImage than the label is named userImageFilename)

### Dropdown lists
EasyPI supports passing a dynamic list to be shown in a dropdown. In addition, you can choose the name of the field that will hold value selected by the user.
To support dynamic dropdown lists, follow the following guidelines:

```
<select class="sdpi-item-value select sdProperty sdList" id="sounds" oninput="setSettings()" sdListTextProperty="soundName" sdListValueProperty="soundIndex" sdValueField="soundTitle"></select>
```

1. On the select element, add an additional class named **sdList** _in addition_ to the sdProperty indicated above.
2. Add an attribute named **sdListTextProperty** which is the name of the *property* for each item in the list that holds the text you want to show in the dropdown
3. Add an attribute named **sdListValueProperty** which is the name of the *property* for each item in the list that holds the value you want to return when an item is selected
4. Add an attribute named **sdValueField** which is the name of a property in the payload which will be used to both retreive the selected value and store it back if the user chooses another option in the dropdown.

#### Example of how this would look on the backend side:
```
[JsonProperty(PropertyName = "sounds")]
public List<SoundData> Sounds { get; set; }

[JsonProperty(PropertyName = "soundTitle")]
public string SoundTitle { get; set; }
```
and `SoundData` would look like this:
```
class SoundData
    {
        [JsonProperty(PropertyName = "soundName")]
        public string SoundName { get; set; }

        [JsonProperty(PropertyName = "soundIndex")]
        public int SoundIndex { get; set; }
    }
```


### HTML Elements
EasyPI supports passing HTML to be shown in the InnerHTML property of an element in the Property Inspector.
To support HTML elements, follow the following guidelines:

```
<span class="sdHTML sdProperty sdpi-item-value" id="myHtmlElement"></span>
```

1. On the div/span element, add an additional class named **sdHTML** _in addition_ to the sdProperty indicated above.

## Events
The library currently sends out two events
### websocketCreate
This event will be triggered as soon as a websocket is created with the StreamDeck.
Subscribe by using: 
```
document.addEventListener('websocketCreate', function () {
	console.log("Websocket created!");
	...
});
```

You can also subscribe to the websocket events as part of this event:

```
document.addEventListener('websocketCreate', function () {
	websocket.addEventListener('message', function (event) {
		console.log("Got message event!");
	});
});
```

### settingsUpdated
This even will be triggered when a new payload of settings is sent from the PI to the Plugin itself.
Subscribe by using: 

```
document.addEventListener('settingsUpdated', function (event) {
    console.log("Got settingsUpdated event!");
});
```

### Ranged Tooltip
The rangedTooltip.js library allows you to see a tooltip of the current value when modifying a range element.
To use:
1. Add `<script src="rangeTooltip.js"></script>` and `<link rel="stylesheet" href="rangeTooltip.css">` to the <head> section  
2. Create a div of `type="range"` and give it the `sdShowTooltip` class (as shown below). The example below will create a range of 1-200
```
<div type="range" class="sdpi-item sdShowTooltip" id="dvSpeed">
	<div class="sdpi-item-label" id="speedLabel">Speed</div>
	<div class="sdpi-item-value">
		<span class="clickable" value="1">1</span>
		<input type="range" min="1" max="200" value=100 list="numbers" data-suffix=" %" class="sdProperty" id="playSpeed" oninput="setSettings()">
		<span class="clickable" value="200">200</span>
		<datalist id="numbers">
			<option>100</option>
		</datalist>
		<label for="playSpeed" class="rangeLabel"></label>
	</div>
</div>
```  
3. For the actual range <input> add the `class="sdProperty"` and the `data-suffix`. You can use `data-suffix` to decide what to show after the number (such as %, 'ms', etc.)
4. Add the following line somewhere in your HTML file:  
```
<div class="sdpi-info-label hidden" style="top: -1000;" value="">Tooltip</div>
```
		
