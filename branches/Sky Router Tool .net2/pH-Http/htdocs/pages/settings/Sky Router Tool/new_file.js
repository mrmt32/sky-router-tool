/**
 * @author mrmt32
 */
SendCommandToServer({action:'get_consolidation_rules'}, function(jsonResponse)
{
	if (jsonResponse.isError)
	{
		$("#consolidationForm").html(jsonResponse.ErrorString);
	}
	else
	{
		$("#consolidationForm").submit(function(e)
		{
			e.preventDefault();
			e.stopPropagation();
			
			// Fist find which items where removed:
			var removedItems = "";
			$("tr.disabled").each(function(index, domElement)
			{
				removedItems += (index == 0? "":";") + $(domElement).attr("consolid");
			});
			
			// Now find items that are not removed (so they can be updated or inserted
			var newItems = "";
			$("tr:not(.disabled):has(input)").each(function(index, domElement)
			{
				var id = $(domElement).attr("consolid");
				
				// Calculate time in seconds
				var timeBeforeFormated = $(this).find("input[name=timeBefore]").val();
				var timeBeforeMultiple = $(this).find("select[name=timeBeforeType] option:selected").val();
				var timeBefore = Math.round(timeBeforeFormated * (timeBeforeMultiple / 1000));
				
				var resolutionFormated = $(this).find("input[name=resolution]").val();
				var resolutionMultiple = $(this).find("select[name=resolutionType] option:selected").val();
				var resolution = Math.round(resolutionFormated * (resolutionMultiple / 1000));
				
				newItems += (index == 0? "":";") + timeBefore + ":" + resolution + (isset(id)? ":" + id : "");
			});
			
			$.post(InterfaceLocation + "?action=set_consolidation_rules", {update: newItems, 'delete': removedItems}, function(jsonResponse)
			{
				ParseContentData(window.ContentData.blocks.settings_change_status_block, {jsonResponse:jsonResponse, dontShowValues: true}, function (output)
				{
					$(".settingsPage").html(output);
				});
				
			}, "json");
			
			ParseContentData(window.ContentData.blocks.loading_text_block, {}, function (output)
			{
				$(".settingsPage").html(output);
			});
			return false;
		});
		
		ParseContentData(window.ContentData.blocks.consolidation_settings_form_block, {ReturnData: jsonResponse.ReturnData}, function(output)
		{
			$("#consolidationForm").html(output);
		})
	}
});