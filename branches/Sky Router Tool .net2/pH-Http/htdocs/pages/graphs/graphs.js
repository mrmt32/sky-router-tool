/**
 * @author mrmt32
 */
/* Graphing functions */
pHMb.Graphing = 
{
	ChangeTimeSpan: function (timeSpanString, titleString)
	{
		$(".graphMenu a").removeClass("current");
		switch ( timeSpanString )
		{
			case "lasthour":
				startTime = (new Date().getTime() - (60 * 60 * 1000)) / 1000;
				endTime = (new Date().getTime()) / 1000;
				timeFormat = "%H:%M";
				$("#lasthour").addClass("current");
				$("#pageTitle").html(titleString + " (Last Hour)");
				break;
		
			case "lastday":
				startTime = (new Date().getTime() - (24 * 60 * 60 * 1000)) / 1000;
				endTime = (new Date().getTime()) / 1000;
				timeFormat = "%H:%M";
				$("#lastday").addClass("current");
				$("#pageTitle").html(titleString + " (Last Day)");
				break;
				
			case "lastmonth":
				startTime = (new Date().getTime() - (28 * 24 * 60 * 60 * 1000)) / 1000;
				endTime = (new Date().getTime()) / 1000;
				timeFormat = "%d/%m/%y %H:%M";
				$("#lastmonth").addClass("current");
				$("#pageTitle").html(titleString + " (Last Month)");
				break;
				
			case "lastyear":
				startTime = (new Date().getTime() - (365 * 24 * 60 * 60 * 1000)) / 1000;
				endTime = (new Date().getTime()) / 1000;
				timeFormat = "%d/%m/%y";
				$("#lastyear").addClass("current");
				$("#pageTitle").html(titleString + " (Last Year)");
				break;
				
			case "last10year":
				startTime = (new Date().getTime() - (10 * 365 * 24 * 60 * 60 * 1000)) / 1000;
				endTime = (new Date().getTime()) / 1000;
				timeFormat = "%d/%m/%y";
				$("#last10year").addClass("current");
				$("#pageTitle").html(titleString + " (Last 10 Years)");
				break;
				
			default:
				startTime = (new Date().getTime() - (60 * 60 * 1000)) / 1000;
				endTime = (new Date().getTime()) / 1000;
				timeFormat = "%d/%m/%y %H:%M";
				$("#lasthour").addClass("current");
				$("#pageTitle").html(titleString + " (Last Hour)");
				break;
		}
		return {startTime: startTime, endTime: endTime};
	}
}