<?php
class Section
{
	public $name;
	public $defaultPage;
	public $author;
	public $navigation;
	public $pages;
	public $title;
	public $displayOrder;
}

class Page
{
	public $name;
	public $author;
	public $content;
}

class Block
{
	public $name;
	public $author;
	public $content;
}

$contentData = array();
$javascriptIncludes = "";

foreach ( new RecursiveIteratorIterator( new RecursiveDirectoryIterator($_SERVER['argv'][1])) as $fileInfo)
{
    if ($fileInfo->getBasename() != $fileInfo->getBasename(".html"))
    {
        print("\t" . $fileInfo->getFilename() . "\r\n");
        $content = file_get_contents($fileInfo->getPathname());
        preg_match_all("/<&contentdata(.*?)>(.*?)<&\\/contentdata>/is", $content, $metaDataString, PREG_SET_ORDER);
        foreach ($metaDataString as $scriptItem)
        {
        	$metaData = array();
            // Get meta data
            preg_match_all("/(.*?) *?= *?('|\")(.*?)\\2/i", $scriptItem[1], $metaParams, PREG_SET_ORDER);
            foreach ($metaParams as $metaParam)
            {
            	$metaData[$metaParam[1]] = $metaParam[3];
            }
            
			if (isset($metaData["type"]) && ( isset($metaData["section_name"]) || strtoupper($metaData["type"]) == "BLOCK"))
			{
				switch (strtoupper($metaData["type"]))
				{
					case "SECTION":
						if (!isset($contentData["sections"][$metaData["section_name"]]))
						{
							$contentData["sections"][$metaData["section_name"]] = new Section();
						}
						
						$contentData["sections"][$metaData["section_name"]]->name = $metaData["section_name"];
						$contentData["sections"][$metaData["section_name"]]->defaultPage = $metaData["default_page"];
						$contentData["sections"][$metaData["section_name"]]->author = $metaData["author"];
						$contentData["sections"][$metaData["section_name"]]->title = $metaData["title"];
						$contentData["sections"][$metaData["section_name"]]->displayOrder = $metaData["display_order"];
						
						$contentData["sections"][$metaData["section_name"]]->navigation = new Page();
						$contentData["sections"][$metaData["section_name"]]->navigation->name = "navigation";
						$contentData["sections"][$metaData["section_name"]]->navigation->content = $scriptItem[2];
						$contentData["sections"][$metaData["section_name"]]->navigation->author = $metaData["author"];
						
						print ("\t\tSection '" . $metaData["section_name"] . "' by " . $metaData["author"] . " loaded.\r\n");
						break;
						
					case "PAGE":
						if (!isset($contentData["sections"][$metaData["section_name"]]))
						{
							$contentData["sections"][$metaData["section_name"]] = new Section();
						}
						
						$currentPage = new Page();
						$currentPage->name = $metaData["page_name"];
						$currentPage->author = $metaData["author"];
						$currentPage->content = $scriptItem[2];
						$contentData["sections"][$metaData["section_name"]]->pages[$currentPage->name] = $currentPage;
						print ("\t\tPage '" . $metaData["section_name"] . "," . $metaData["page_name"] . "' by " . $metaData["author"] . " loaded.\r\n");
						break;
						
					case "BLOCK":
						$currentBlock = new Block();
						$currentBlock->name = $metaData["block_name"];
						$currentBlock->author = $metaData["author"];
						$currentBlock->content = $scriptItem[2];
						$contentData["blocks"][$currentBlock->name] = $currentBlock;
						print ("\t\tBlock '" . $metaData["block_name"] . "' by " . $metaData["author"] . " loaded.\r\n");
						break;
				}
			}  
        }
    }
	else if ($fileInfo->getBasename() != $fileInfo->getBasename(".js"))
	{
		$javascriptIncludes = $javascriptIncludes . "\r\n" . file_get_contents($fileInfo->getPathname());
		print ("\tJavascript file '" . $fileInfo->getBasename() . "' loaded.\r\n");
	}
}

file_put_contents(trim($_SERVER['argv'][2], "\\") . "\\" . "includes.js", $javascriptIncludes);
file_put_contents(trim($_SERVER['argv'][2], "\\") . "\\" . "contentData.json", json_encode($contentData));
?>
