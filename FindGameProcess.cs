using System;
using System.Diagnostics;
using System.Security.Principal;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

public class CPHInline
{	
	int timeout = 30000;
	int retries = 5;
	string[] procToIgnore = new string[]{"explorer", "svchost", "searchapp", "steam", "firefox", "chrome", "discord", "obs64"};
	string[] catsToIgnore = new string[]{"steam"};
	Regex rgx = new Regex("\\s");
	
	string GameName = "";
	Process GameProcess = null;

	public void Init()
	{		
		Execute();
	}

	public bool Execute()
	{
		CPH.LogDebug("StreamerBot is watching processes");
		while (retries > 0) {
			GetGameNameAndProcess();
			if (GameName != "" && GameProcess != null)
			{
				CPH.LogDebug(String.Format("Game match: {0}", GameName));
				try
				{
					CPH.SetChannelGame(GameName);
				}catch{}			
				GameProcess.WaitForExit();
				CPH.LogDebug(String.Format("Process {0} exited", GameProcess.ProcessName));
				retries = 5;
			} else {
				CPH.LogDebug("Can't match by any criteria");
				retries--;			
			}			
			CPH.LogDebug(String.Format("Waiting for new attempt: {0} ms", timeout));
			Thread.Sleep(timeout);
		}
		CPH.LogDebug("StreamerBot stopped watching processes");
		return false;
	}

	void GetGameNameAndProcess()
	{
		CPH.LogDebug(String.Format("Retires left: {0}", retries));
		GameName = "";
		GameProcess = null;

		Process[] processArr = GetUserProcesses();
		List<string> gamesList = GetGames();

		if(MatchByProcess(gamesList, processArr)) return;
		CPH.LogDebug("Can't match by process");

		if (MatchByTitle(gamesList, processArr)) return;
		CPH.LogDebug("Can't match by title");

		if (MatchByFolder(gamesList, processArr)) return;
		CPH.LogDebug("Can't match by folder");	

		if (MatchByProductName(gamesList, processArr))	return;
		CPH.LogDebug("Can't match by product name");
	}

	Process[] GetUserProcesses()
	{
		CPH.LogDebug("Getting user processes");
		Process[] processList = Process.GetProcesses();
		int currentSessionID = Process.GetCurrentProcess().SessionId;
		Process[] userProcesses = new Process[]{};
        return Array.FindAll(processList, p => {
			return isCurrentSession(p, currentSessionID) && hasAccess(p) && hasWindow(p) && !Array.Exists(procToIgnore, i => i == NormalizeName(p.ProcessName));
		});
	}

	List<string> GetGames()
	{		
		CPH.LogDebug("Getting game list");
		string filePath = @"data\games.dat";
		string jsonStr = File.ReadAllText(filePath);

		var gamesJson = JArray.Parse(jsonStr);

		var gamesList = new List<string>();		

		foreach (JObject game in gamesJson)
		{
			var gameName = game["name"].ToString();
			if (Array.Exists(catsToIgnore, i => i == NormalizeName(gameName))) continue;
			gamesList.Add(gameName);
		}

		return gamesList;
	}

	bool MatchByProcess(List<string> gamesList, Process[] processArr)
	{
		CPH.LogDebug("Trying to find a game by process name");
		return gamesList.Exists(g => Array.Exists(processArr, p =>
		{
			if (NormalizeName(p.ProcessName) == NormalizeName(g))
			{
				GameName = g;
				GameProcess = p;
				return true;
			}
			return false;
		}
		));
	}

	bool MatchByTitle(List<string> gamesList, Process[] processArr)
	{
		CPH.LogDebug("Trying to find a game by title");
		return gamesList.Exists(g => Array.Exists(processArr, p =>
		{
			if (NormalizeName(p.MainWindowTitle) == NormalizeName(g))
			{
				GameName = g;
				GameProcess = p;
				return true;
			}
			return false;
		}
		));
	}

	bool MatchByFolder(List<string> gamesList, Process[] processArr)
	{
		CPH.LogDebug("Trying to find a game by folder");
		return gamesList.Exists(g => Array.Exists(processArr, proc =>
		{
			string[] pathArr = proc.MainModule.FileName.Split(Path.DirectorySeparatorChar);
			return Array.Exists(pathArr, p =>
			{
				if (NormalizeName(p) == NormalizeName(g))
				{
					GameName = g;
					GameProcess = proc;
					return true;
				}
				return false;
			}
			);
		}			
		));
	}

	bool MatchByProductName(List<string> gamesList, Process[] processArr)
	{
		CPH.LogDebug("Trying to find a game by product name");
		return gamesList.Exists(g => Array.Exists(processArr, p =>
		{
			if (hasProductName(p) && NormalizeName(p.MainModule.FileVersionInfo.ProductName) == NormalizeName(g))
			{
				GameName = g;
				GameProcess = p;
				return true;
			}
			return false;
		}
		));
	}

	bool isCurrentSession(Process p, int sessionID)
	{
		return p.SessionId == sessionID;
	}

	bool hasAccess(Process p)
	{
		try
		{
			var mainModule = p.MainModule;
			return true;
		}
		catch
		{
			return false;
		}
	}

	bool hasWindow(Process p)
	{
		return p.MainWindowHandle != IntPtr.Zero;
	}

	bool hasProductName(Process p)
	{
		return p.MainModule.FileVersionInfo.ProductName != null;
	}

	string NormalizeName(string input)
	{
		return rgx.Replace(input, "").ToLower();
	}
}
