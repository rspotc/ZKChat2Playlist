using System.Collections.Generic;

public class CommandsHelpPages
{
	public CommandsHelpPages()
	{
		commandPages = new List<string>();
	}

	public string getPage()
	{
		if (commandPages.Count == 0) return "";
		else
		{
			return commandPages[currentPage];
		}
	}

	public void pageChange()
	{
		if (commandPages.Count == 0) return;

		currentPage = ((currentPage + 1) % commandPages.Count);
	}

	public int getCurrentPage() { return currentPage; }
	public int getPageCount() { return commandPages.Count; }

	public void addPage(string commands = "")
	{
		if (commandPages.Count == 0) currentPage = 0;
		commandPages.Add(commands);
	}

	public void addCommandGroup(string commands, int page=1)
	{
		if (commandPages.Count < page)
		{
			addPage(commands);
			return;
		}

		if (commandPages[page - 1] != "") commandPages[page - 1] += "\n\n\n";
		commandPages[page - 1] += commands;
	}

	private List<string> commandPages;
	private int currentPage;
}
