var capturePeriod = 1800;
var firstCapture = new DateTimeOffset(2018, 4, 27, 9, 30, 0, TimeSpan.Zero);  
var capturePeriodTicks = TimeSpan.FromSeconds(capturePeriod).Ticks;
var dateNow = new DateTimeOffset(2018, 4, 27, 14, 0, 0, TimeSpan.Zero);  
var numberOfCaptures = (int)((dateNow.Subtract(DateTimeOffset.MinValue).TotalSeconds) / capturePeriod); 

var lastCaptureTime = new DateTimeOffset(numberOfCaptures * capturePeriodTicks, dateNow.Offset);
lastCaptureTime.Dump();

var diff = lastCaptureTime - firstCapture;
var d = 

(diff.TotalSeconds/capturePeriod + 1).Dump();
Enumerable.Range(0, 10)
