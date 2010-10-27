using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CoApp.Scan.Types;

namespace CoApp.Scan
{
	/// <summary>
	/// Scans an open source project to gather information.
	/// </summary>
	public class ProjectScanner
	{
		#region Private Fields --------------------------------------------------------------------------------------------------
		private bool _verbose = false;
		#endregion --------------------------------------------------------------------------------------------------------------

		#region Properties ------------------------------------------------------------------------------------------------------
		/// <summary>
		/// Enables or disables verbose mode which can show more output.
		/// </summary>
		/// <text><c>true</c> if verbose mode enabled; otherwise, <c>false</c>.</text>
		public bool Verbose
		{
			get { return _verbose; }
			set { _verbose = value; }
		}

		#endregion --------------------------------------------------------------------------------------------------------------

		#region Constructors ----------------------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes a new instance of the <see cref="ProjectScanner"/> class.
		/// </summary>
		public ProjectScanner()
		{
		}
		#endregion --------------------------------------------------------------------------------------------------------------

		#region Public Methods --------------------------------------------------------------------------------------------------
		/// <summary>
		/// Performs the scan of the open source project and builds the report.
		/// </summary>
		/// <returns>A ScanReport of the project.</returns>
		public ScanReport Scan(string rootDirectory)
		{
			ScanReport report = new ScanReport();

			rootDirectory = Path.GetFullPath(rootDirectory).TrimEnd('\\');

			//
			// Build the list of files
			//
			ScanDirectory(report, rootDirectory, rootDirectory);

			//
			// Analyze the files that have been found
			//
			AnalyzeFiles(report);

			return report;
		}
		#endregion --------------------------------------------------------------------------------------------------------------

		#region Private Methods -------------------------------------------------------------------------------------------------

		/// <summary>
		/// Analyzes all the files found in the given scan report.  Each file is checked for 
		/// definitions and includes.  Updates are saved in the report.
		/// </summary>
		/// <param name="report">The report to scan.</param>
		private void AnalyzeFiles(ScanReport report)
		{
			var sourceFiles = report.FilesArray.Where<ScannedFile>(x => (x.Type == ScannedFileType.C));

			foreach (ScannedFile file in sourceFiles)
			{
				if (Verbose) Console.WriteLine("Analyzing {0}", file);
				AnalyzeDefines(report, file);
				AnalyzeIncludes(report, file);
			}
		}

		/// <summary>
		/// Analyzes the defines in the given file.  Information is saved in the report.
		/// </summary>
		/// <param name="report">The report to save the results in.</param>
		/// <param name="file">The file to analyze.</param>
		private void AnalyzeDefines(ScanReport report, ScannedFile file)
		{
			string[] fileContents;
			Match m;
			int lineNumber = 0;

			fileContents = File.ReadAllLines(file.FullName);

			foreach (string line in fileContents)
			{
				lineNumber++;

				#region Search For define directives
				m = Regex.Match(line, @"^\s*#\s*define\s+(\w+)\s+(.*)$", RegexOptions.IgnoreCase);
				if (m.Success)
				{
					string identifier = m.Groups[1].ToString();
					string value = m.Groups[2].ToString();

					value = StripComments(value);

					if (Verbose) Console.WriteLine("  Line {0,-7}: Found {3,-18}: {1} as {2}", lineNumber, identifier, value, "define");
					report.AddDefineValue(identifier, value, file.ID);
				}
				#endregion

				#region Search for ifdef, ifndef, and undef
				foreach (string pattern in new string[] { @"^\s*#\s*ifdef\s+(\w+)", @"^\s*#\s*ifndef\s+(\w+)", @"^\s*#\s*undef\s+(\w+)" })
				{
					m = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
					if (m.Success)
					{
						string identifier = m.Groups[1].ToString();
						if (Verbose) Console.WriteLine("  Line {0,-7}: Found {2,-18}: {1}", lineNumber, identifier, "ifdef/ifnef/undef");
						report.AddDefine(identifier, file.ID);
					}
				}
				#endregion

				#region Search for if and elif
				foreach (string pattern in new string[] { @"^\s*#\s*if\s+(.*)", @"^\s*#\s*elif\s+(.*)" })
				{
					m = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
					if (m.Success)
					{
						string text = m.Groups[1].ToString();
						text = StripComments(text);

						foreach (Match definedMatch in Regex.Matches(text, @"defined\((\w+)\)", RegexOptions.IgnoreCase))
						{
							string identifier = definedMatch.Groups[1].ToString();
							if (Verbose) Console.WriteLine("  Line {0,-7}: Found {2,-18}: {1}", lineNumber, identifier, "if/elif");
							report.AddDefine(identifier, file.ID);
						}
					}
				}

				#endregion
			}
		}

		/// <summary>
		/// Analyzes the files for include statements.  Information is saved in the report.
		/// </summary>
		/// <param name="report">The report to save the results in.</param>
		/// <param name="file">The file to analyze.</param>
		private void AnalyzeIncludes(ScanReport report, ScannedFile file)
		{
		}

		/// <summary>
		/// Recursively scans the project source directory for all files.  Each is classified
		/// and written to the report structure.
		/// </summary>
		/// <param name="report">The report to save the results in.</param>
		/// <param name="rootDirectory">The root directory of the project.</param>
		/// <param name="currentSearchDirectory">The current search directory.</param>
		private void ScanDirectory(ScanReport report, string rootDirectory, string currentSearchDirectory)
		{
			FileSystemInfo[] files = null;
			DirectoryInfo dirInfo = null;

			if (Directory.Exists(currentSearchDirectory) == false) return;

			dirInfo = new DirectoryInfo(currentSearchDirectory);
			files = dirInfo.GetFileSystemInfos();

			foreach (FileSystemInfo item in files)
			{
				FileInfo fileInfo = item as FileInfo;
				if (fileInfo != null)
				{
					ScannedFile file = new ScannedFile();
					file.Directory = ShortenPath(rootDirectory, fileInfo.DirectoryName);
					file.FullName = fileInfo.FullName;
					file.Name = fileInfo.Name;
					file.Type = ScannedFile.DetermineFileType(fileInfo.Name);

					if (Verbose) Console.WriteLine("Adding {0}", file);
					report.AddFile(file);
				}
			}

			foreach (FileSystemInfo item in files)
			{
				DirectoryInfo dir = item as DirectoryInfo;
				if (dir != null)
				{
					if (dir.Name == "." || dir.Name == "..") continue;

					ScanDirectory(report, rootDirectory, dir.FullName);
				}
			}
		}

		/// <summary>
		/// Shortens the file path by removing the root directory from the begining of it.
		/// </summary>
		/// <param name="rootPath">The root path.</param>
		/// <param name="inputPath">The input path.</param>
		/// <returns>A path without the root path on the front.</returns>
		private string ShortenPath(string rootPath, string inputPath)
		{
			if (inputPath.StartsWith(rootPath))
			{
				inputPath =  inputPath.Substring(rootPath.Length);
				if (inputPath.StartsWith("\\")) inputPath = inputPath.Substring(1);
			}
			return inputPath;
		}

		/// <summary>
		/// Strips any C comments from a text line.  This is not perfect but should
		/// catch the conditions given to it by the Analyze methods.
		/// </summary>
		/// <param name="text">The text to search.</param>
		/// <returns>The text text without comments and outside whitespace.</returns>
		private string StripComments(string text)
		{
			text = Regex.Replace(text, @"/\*.*?\*/", "");
			text = Regex.Replace(text, @"/\*.*$", "");
			text = text.Trim();

			return text;
		}

		#endregion --------------------------------------------------------------------------------------------------------------
	}
}
