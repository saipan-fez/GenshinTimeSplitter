
# Output Files

## Usage Example

1. Open the CSV file with a spreadsheet tool like Excel.
1. Open the XSPF file with a video player.
1. Manually record the number of elite enemies defeated in the Excel file while seeking through the videos in the playlist.
1. Calculate the time efficiency per elite from the time per section and the number of elite enemies defeated.

## File Specifications

### CSV File

The specifications for each column are as follows:

|Column Name|Description|
|:-|:-|
|no|Sequence number (assigned sequentially starting from 1)|
|section_start|Time within the video when the section started|
|load_start|Time within the video when the loading screen appeared|
|seconds_from_section_start_to_load_start|Time from the start of the section to the appearance of the loading screen (in seconds)|

### XSPF File

This is an XML format playlist file.  
It can be opened with video players like [VLC MediaPlayer](https://www.videolan.org/vlc/).  

By configuring VLC MediaPlayer as shown below, you can smoothly tally the elite enemies:

1. From the [View] menu, set the following:
    1. Display the playlist by selecting [Playlist]
    1. Choose "Detailed List" in [Playlist View Mode]
    1. Set [Dock Playlist] to "ON"

Example with the settings applied
![](./img/xspf_usage.png)

### JSON File

The specifications for each Key are as follows:

|Key|Description|
|:-|:-|
|AnalyzeStartTimeSpan|Start time specified in the app's `Analyze Range`|
|AnalyzeEndTimeSpan|End time specified in the app's `Analyze Range`|
|Sections||
|　No|Sequence number (assigned sequentially starting from 1)|
|　SectionStartedTimeSpan|Time within the video when the section started|
|　MapOpenedTimeSpan|(reserved) always null|
|　LoadScreenStartedTimeSpan|Time within the video when the loading screen appeared|
|　SecondsFromSectionStartToMapOpened|(reserved) always null|
|　SecondsFromSectionStartToLoadScreenStarted|Time from the start of the section to the appearance of the loading screen (in seconds)|
