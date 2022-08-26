using System;
using System.Diagnostics;
using System.Security.Principal;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Runtime.InteropServices;

public class CPHInline
{	
	[DllImport("user32.dll")]
	static extern IntPtr GetForegroundWindow();
	[DllImport("User32.dll")]
	static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

	int timeout = 1000;
	int retries = 2;
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
		
		IntPtr fHndl = GetForegroundWindow();
		uint processID;
		GetWindowThreadProcessId(fHndl, out processID);
		Process targetProcess = Process.GetProcessById((int)processID);
		if(Array.Exists(procToIgnore, i => i == NormalizeName(targetProcess.ProcessName))) return;

		List<string> gamesList = GetGames();

		if(MatchByProcess(gamesList, targetProcess)) return;
		CPH.LogDebug("Can't match by process");

		if (MatchByTitle(gamesList, targetProcess)) return;
		CPH.LogDebug("Can't match by title");

		if (MatchByFolder(gamesList, targetProcess)) return;
		CPH.LogDebug("Can't match by folder");	

		if (MatchByProductName(gamesList, targetProcess))	return;
		CPH.LogDebug("Can't match by product name");

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

	bool MatchByProcess(List<string> gamesList, Process process)
	{
		CPH.LogDebug("Trying to find a game by process name");
		return gamesList.Exists(g =>
		{
			if (NormalizeName(process.ProcessName) == NormalizeName(g))
			{
				GameName = g;
				GameProcess = process;
				return true;
			}
			return false;
		}
		);
	}

	bool MatchByTitle(List<string> gamesList, Process process)
	{
		CPH.LogDebug("Trying to find a game by title");
		return gamesList.Exists(g => 
		{
			if (NormalizeName(process.MainWindowTitle) == NormalizeName(g))
			{
				GameName = g;
				GameProcess = process;
				return true;
			}
			return false;
		}
		);
	}

	bool MatchByFolder(List<string> gamesList, Process targetProcess)
	{
		CPH.LogDebug("Trying to find a game by folder");
		return gamesList.Exists(g =>
		{
			string[] pathArr = targetProcess.MainModule.FileName.Split(Path.DirectorySeparatorChar);
			return Array.Exists(pathArr, p =>
			{
				if (NormalizeName(p) == NormalizeName(g))
				{
					GameName = g;
					GameProcess = targetProcess;
					return true;
				}
				return false;
			}
			);
		}			
		);
	}

	bool MatchByProductName(List<string> gamesList, Process targetProcess)
	{
		CPH.LogDebug("Trying to find a game by product name");
		return gamesList.Exists(g =>
		{
			if (hasProductName(targetProcess) && NormalizeName(targetProcess.MainModule.FileVersionInfo.ProductName) == NormalizeName(g))
			{
				GameName = g;
				GameProcess = targetProcess;
				return true;
			}
			return false;
		}
		);
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
