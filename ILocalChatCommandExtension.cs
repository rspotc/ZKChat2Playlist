using System;
using System.Collections.Generic;
namespace ZeepSDK.ChatCommands; 

public class ILocalChatCommandExtension {

	public ILocalChatCommandExtension(string prefix_, string command_, string description_, Action<string> defaultHandle_ = null, bool isSubCommand_=false, params string[] arguments_)
	{
		prefixVal = prefix_;
		commandVal = command_;
		descriptionVal = description_;
		defaultHandle = defaultHandle_;
		aliases = [];
		arguments = [.. arguments_];
		subCommands = [];

		isSubCommand = isSubCommand_;
		if (!isSubCommand) ChatCommandApi.RegisterLocalChatCommand(prefixVal, commandVal, "Press F1 for command help", Handle);
	}

	public void addAlias(string alias)
	{
		aliases.Add(alias);
		if (!isSubCommand) ChatCommandApi.RegisterLocalChatCommand(prefixVal, alias, $"Alias of {prefixVal}{commandVal}", Handle);
	}

	public bool isAlias(string aliasQuery)
	{
		if (aliasQuery == commandVal) return true;
		
		foreach (string alias in aliases)
		{
			if (alias == aliasQuery) return true;
		}
		return false;
	}

	public void registerSubcommand(string function, string description, string[] subAliases = null, Action<string> handle = null, params string[] arguments)
	{
		ILocalChatCommandExtension subcommand = new ILocalChatCommandExtension("", function, description, handle, true, arguments);

		subAliases ??= [];
		foreach (string alias in subAliases)
		{
			subcommand.addAlias(alias);
		}

		subCommands.Add(subcommand);
	}

	public string repr()
	{
		string commandsString = "";
		if (!isSubCommand && (subCommands.Count > 0 || aliases.Count > 0))
		{
			commandsString += $"<color=#FFDD00>{prefixVal}{commandVal}";
			foreach (string alias in aliases)
			{
				commandsString += $" | {prefixVal}{alias}";
			}
			commandsString += "</color>";

			if (subCommands.Count == 0 && arguments.Count == 0)
			{
				commandsString += $" <color=#FF8800>-- {descriptionVal}</color>";
			}

			foreach (ILocalChatCommandExtension subcommand in subCommands)
			{
				commandsString += $"\n{prefixVal}{commandVal} {subcommand.repr()}";
			}

			if (arguments.Count > 0)
			{
				commandsString += $"\n{prefixVal}{commandVal}";
				foreach (string arg in arguments)
				{
					commandsString += $" [{arg}]";
				}
				commandsString += $" <color=#FF8800>-- {descriptionVal}</color>";
			}
		}
		else
		{
			commandsString += $"{commandVal}";

			foreach (string arg in arguments)
			{
				commandsString += $" [{arg}]";
			}

			commandsString += $" <color=#FF8800>-- {descriptionVal}</color>";

			for (int aliasIdx=0; aliasIdx < aliases.Count; ++aliasIdx)
			{
				if (aliasIdx == 0)
				{
					commandsString += " <color=#407AFF>(aliases;";
				}

				commandsString += $" {aliases[aliasIdx]}";

				if (aliasIdx == aliases.Count - 1)
				{
					commandsString += ")</color>";
				}
				else
				{
					commandsString += ",";
				}
			}
		}
		return commandsString;
	}

	private void Handle(string arguments)
	{
		string[] commandFunc = arguments.Split(' ', 2);
		foreach (ILocalChatCommandExtension subcommand in subCommands)
		{
			if (subcommand.isAlias(commandFunc[0]))
			{
				subcommand.Handle(commandFunc.Length == 2 ? commandFunc[1] : "");
				return;
			}
		}
		defaultHandle?.Invoke(arguments);
	}

	private string prefixVal;
	private string descriptionVal;
	private string commandVal;
	private List<string> aliases;
	private List<string> arguments;
	private List<ILocalChatCommandExtension> subCommands;
	private Action<string> defaultHandle;
	private bool isSubCommand;
}
