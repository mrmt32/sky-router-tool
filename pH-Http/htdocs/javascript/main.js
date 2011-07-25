$().ready(function()
{
    // Download content data
    $.getJSON("contentData.json", function(data)
    {
		window.ContentData = data;
		
		// Check which router version we are dealing with 
		// (so that menus can be customised)
		SendCommandToServer({action:'get_settings', settings:'RouterModel'}, function(jsonResponse)
		{
			if (jsonResponse.isError)
			{
				alert(jsonResponse.ErrorString);
			}
			else
			{
				window.SrtSettings = 
				{
					RouterModel: jsonResponse.ReturnData.RouterModel
				};
				
				sections = ObjectToArray(window.ContentData.sections).sort(sortByDisplayOrder);
				var i = 0;
				for (sectionId in sections)
				{
					var currentSection = sections[sectionId];
					
					if (i == sections.length - 1) 
					{
						$(".mainMenu").append("<li class='lastItem'><a href='#" + currentSection.name + "' id='" + currentSection.name + "'>" + currentSection.title + "</a></li>");
					}
					else 
					{
						$(".mainMenu").append("<li><a href='#" + currentSection.name + "' id='" + currentSection.name + "'>" + currentSection.title + "</a></li>");
					}
					i++;
				}
				
				setInterval("CheckPageChange()", 300);
			}
		});


    })
});

var InterfaceLocation = "/interface.json";
var newsItemsPerPage = 5;
var currentAnchor = null;
var pHMb = {};

function sortByDisplayOrder(a, b)
{
	var x = a.displayOrder
	var y = b.displayOrder
	return ((x < y) ? -1 : ((x > y) ? 1 : 0));
}

/**
 * Checks to see if a variable is defined
 * @param {Object} variable The variable to check
 */
function isset(variable)
{
    var undefined;
    return (variable == undefined ? false : true);
}

/**
 * Counts the number of elements in an object
 * @param {Object} object The object to count
 */
function CountObject(object)
{
	var i = 0;
	for (name in object) 
	{
		if(object.hasOwnProperty(name))
			i++;
	}
	
	return i;
}

/**
 * Converts an object to an array
 * @param {Object} object The object to convert
 */
function ObjectToArray(object)
{
	var objArray = new Array();
	var i = 0;
	
	for (param in object)
	{
		objArray[i] = object[param];
		i++;
	}
	
	return objArray;
}

/**
 * Converts a mysql timestamp to a javascript date object
 * @param {String} timestamp The mysql timestamp 
 */
function MysqlTimeStampToDate(timestamp)
{
    var regex = /^([0-9]{2,4})-([0-1][0-9])-([0-3][0-9]) (?:([0-2][0-9]):([0-5][0-9]):([0-5][0-9]))?$/;
    var parts = timestamp.replace(regex, "$1 $2 $3 $4 $5 $6").split(' ');
    return new Date(parts[0], parts[1] - 1, parts[2], parts[3], parts[4], parts[5]);
}

function FormatDate(date)
{
	var weekday=new Array(7);
	weekday[0]="Sunday";
	weekday[1]="Monday";
	weekday[2]="Tuesday";
	weekday[3]="Wednesday";
	weekday[4]="Thursday";
	weekday[5]="Friday";
	weekday[6]="Saturday";
	
	var months = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
	var dateAppend = "th";
	
	if ((date.getDate() - 1) % 10 == 0)
	{
		dateAppend = "st";
	}
	else if((date.getDate() - 2) % 10 == 0)
	{
		dateAppend = "nd";
	}
	else if((date.getDate() - 3) % 10 == 0)
	{
		dateAppend = "rd";
	}
	
	return weekday[date.getDay()] + " " + date.getDate() + dateAppend + " " + months[date.getMonth()] + " " + date.getFullYear();
}

/**
 * Checks to see if the page has been changed
 */
function CheckPageChange()
{
    if (currentAnchor != document.location.hash.substring(1)) 
    {
        //Page has changed!
        currentAnchor = document.location.hash.substring(1);
		
		// Change the page
        ChangePage(currentAnchor);
    }
}

/**
 * Changes the page contents
 * @param {String} nextpage The querystring
 */
function ChangePage(querystring)
{
	var param;
	var params = [];
	var pageName;
	var sectionName;
	
    var paramsEncoded = querystring.split("&");
	
	// Loop through parameters
	for (param in paramsEncoded)
	{
		if (param == 0) 
		{
			// This is the page name and section name
			var sectionPageArray = paramsEncoded[param].split(",")
			
			sectionName = sectionPageArray[0];
			pageName = sectionPageArray[1];
		}
		else 
		{
			var temp = paramsEncoded[param].split("=");
			params[temp[0]] = temp[1];
		}
	}
	
	if (sectionName != "") 
	{
		LoadPage(sectionName, pageName, params);
	}
	else
	{
		LoadPage("home");
	}	
}

function LoadPage(sectionName, pageName, parameters)
{
	//$(".body").slideUp();
	$(".loadingBox").fadeIn();
	
	if (!isset(ContentData.sections[sectionName])) 
	{
		alert("Page not found!");
		window.location.hash = "#home";
		return;
	}
	else 
	{
		if (!isset(pageName)) 
		{
			pageName = ContentData.sections[sectionName].defaultPage;
		}
		
		if (!isset(ContentData.sections[sectionName].pages[pageName])) 
		{
			alert("Page not found!");
			window.location.hash = "#" + sectionName;
			return;
		}
		else 
		{
			$(".mainMenu a").removeClass("current");
			$("#" + sectionName).addClass("current");
			document.title = "Sky Router Tool Beta - " + ContentData.sections[sectionName].title;
			
			ParseContentData(ContentData.sections[sectionName].navigation, parameters, function(output)
			{
				$("#title").html(ContentData.sections[sectionName].title);
				$("#sidebar").html(output);
			});
			
			ParseContentData(ContentData.sections[sectionName].pages[pageName], parameters, function(output)
			{
				$("#page").html(output);
				$(".body").slideDown();
				$(".loadingBox").fadeOut();
			});
		}
	}
}

/**
 * Parse a content data script and return the resulting HTML
 * @param {Object} contentData The content data script to parse
 * @param {Object} parameters The parameters to load into the template
 * @param {Function} callback Provides a callback for the script to call on completion which will return the html
 */
function ParseContentData(contentData, parameters, callback)
{
    var outputHtml = contentData.content;
    
    // Run any embedded scripts
	// Set up environment
	var cdscript = { OwnHtml: outputHtml, ThisPage: contentData };
		
    var regex = /<&javascript>([\s\S]*?)<&\/javascript>/
	var result = regex.exec(outputHtml);
    while (result != null) 
    {
        output = "";
        eval(result[1]);
		
		outputHtml = outputHtml.replace(result[0], output);
        result = regex.exec(outputHtml);
    }
	
	// Insert parameters
	for (parameter in parameters) 
	{
		outputHtml = outputHtml.replace(new RegExp("[\\{<]\\$?" + parameter + "[\\}>]", "gi"), parameters[parameter]);
	}
	
	// Insert blocks
	var regex = /<\$\$(.*?):(.*?)>/;
	var result = regex.exec(outputHtml);
	while (result != null) 
    {
		outputHtml = outputHtml.replace(result[0], ParseContentData(ContentData.blocks[result[1]], eval("(" + result[2] + ")"), null));
        result = regex.exec(outputHtml);
    }
	
	// Remove any unsed parameters
	outputHtml = outputHtml.replace(/\{\$.*?\}/g, "");

    if (callback != null)
	{
		callback(outputHtml.replace(/<&javascript delayed='true'>([\s\S]*?)<&\/javascript>/g, ""));
		
		// Find delayed-load scripts
		// Note that delayed-load scripts are not supported for blocks loaded using a quick tag (<$$xxx>)
		var delayedLoads = new Array();
		var i = 0;
		var regex = /<&javascript delayed='true'>([\s\S]*?)<&\/javascript>/g;
		var result = regex.exec(outputHtml);
	    while (result != null) 
	    {
			eval(result[1]);
			result = regex.exec(outputHtml);
	    }
	}
	else
	{
		return outputHtml;
	}
}

function SendCommandToServer(parameters, callback)
{
	$.getJSON(window.InterfaceLocation, parameters, function(jsonResponse, textStatus)
	{
		if (textStatus == "success")
		{
			callback(jsonResponse);
		}
		else
		{
			callback({isError: true, ErrorString: "Error: Server returned '" + textStatus + "' while trying to get data."});
		}
	})
}
