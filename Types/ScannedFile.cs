using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace CoApp.Scan.Types
{
	/// <summary>
	/// Contains information about a scanned file.
	/// </summary>
	[XmlRoot("file")]
	public class ScannedFile
	{
		/// <summary>
		/// Gets or sets the numeric ID of the file.
		/// </summary>
		/// <value>The ID.</value>
		[XmlAttribute("id")]
		public int ID { get; set; }

		/// <summary>
		/// Gets or sets the directory the file was found in.
		/// </summary>
		/// <value>The directory.</value>
		[XmlAttribute("path")]
		public string Directory { get; set; }

		/// <summary>
		/// Gets or sets the full name of the file.  This is used internally only.
		/// </summary>
		/// <value>The full name.</value>
		[XmlIgnore]
		internal string FullName { get; set; }

		/// <summary>
		/// Gets or sets the name of the file.
		/// </summary>
		/// <value>The name.</value>
		[XmlAttribute("name")]
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the type of file.
		/// </summary>
		/// <value>The type.</value>
		[XmlAttribute("type")]
		public ScannedFileType Type { get; set; }

		//[XmlArray("includes"), XmlArrayItem("fileid", typeof(int))]
		[XmlIgnore]
		public List<int> Includes { get; set; }

		//[XmlArray("includedby"),XmlArrayItem("fileid", typeof(int))]
		[XmlIgnore]
		public List<int> IncludedBy { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ScannedFile"/> class.
		/// </summary>
		public ScannedFile()
		{
			ID = 0;
			Includes = new List<int>();
		}

		/// <summary>
		/// Determines the type of the file by checking extensions and full names.
		/// </summary>
		/// <param name="fileName">Name of the file.</param>
		/// <returns>The type of the file</returns>
		public static ScannedFileType DetermineFileType(string fileName)
		{
			fileName = fileName.ToLower();

			string extension = Path.GetExtension(fileName);

			if (_knownExtensions.ContainsKey(extension))
			{
				return _knownExtensions[extension];
			}

			if (fileName == "install" || fileName.StartsWith("install.")) return ScannedFileType.Document;
			if (fileName == "readme" || fileName.StartsWith("readme.")) return ScannedFileType.Document;
			if (fileName == "makefile" || fileName.StartsWith("makefile.")) return ScannedFileType.BuildFile;
			if (fileName == "build.xml") return ScannedFileType.BuildFile;
			if (fileName == "license" || fileName == "faq" || fileName == "news" || fileName == "problems" || fileName == "issues") return ScannedFileType.Document;
			if (fileName == "changes" || fileName.StartsWith("changes.")) return ScannedFileType.Document;
			if (fileName == "config" || fileName == "configure" || fileName.StartsWith("configure.")) return ScannedFileType.BuildFile;
			if (fileName == "copyright" || fileName == "version") return ScannedFileType.Document;

			return ScannedFileType.Unknown;
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents this instance.
		/// </summary>
		/// <returns>
		/// A <see cref="System.String"/> that represents this instance.
		/// </returns>
		public override string ToString()
		{
			return string.Format("File - Name: {0}, Path: {1}, Type: {2}", Name, Directory, Type);
		}

		#region Static Information ----------------------------------------------------------------------------------------------
		private static Dictionary<string, ScannedFileType> _knownExtensions = new Dictionary<string, ScannedFileType>()
		{
			{ ".c", ScannedFileType.C },
			{ ".cpp", ScannedFileType.C},
			{ ".cxx", ScannedFileType.C},
			{ ".h", ScannedFileType.C},
			{ ".hpp", ScannedFileType.C},
			{ ".hxx", ScannedFileType.C},
			{ ".cc", ScannedFileType.C},
			{ ".hh", ScannedFileType.C},
			{ ".rc", ScannedFileType.Source},
			{ ".asm", ScannedFileType.Assembly},
			{ ".pas", ScannedFileType.Pascal},
			{ ".inc", ScannedFileType.Pascal},
			{ ".xs", ScannedFileType.Source},
			{ ".s", ScannedFileType.Source},

			{ ".cs", ScannedFileType.CSharp},
			{ ".vb", ScannedFileType.VB},
			{ ".pdb", ScannedFileType.Debug},
			{ ".obj", ScannedFileType.Object},

			{ ".bat", ScannedFileType.Script},
			{ ".js", ScannedFileType.Script},
			{ ".vbs", ScannedFileType.Script},
			{ ".sh", ScannedFileType.Script},
			{ ".ps1", ScannedFileType.Script},
			{ ".wsh", ScannedFileType.Script},
			{ ".py", ScannedFileType.Script},
			{ ".pl", ScannedFileType.Script},
			{ ".pm", ScannedFileType.Script},
			{ ".pod", ScannedFileType.Script},
			{ ".pem", ScannedFileType.Script},
			{ ".php", ScannedFileType.Script},
			{ ".phps", ScannedFileType.Script},
			{ ".m4", ScannedFileType.Script},

			{ ".png", ScannedFileType.Media},
			{ ".gif", ScannedFileType.Media},
			{ ".avi", ScannedFileType.Media},
			{ ".mpg", ScannedFileType.Media},
			{ ".mp2", ScannedFileType.Media},
			{ ".mp3", ScannedFileType.Media},
			{ ".mp4", ScannedFileType.Media},
			{ ".mkv", ScannedFileType.Media},
			{ ".ico", ScannedFileType.Media},
			{ ".wav", ScannedFileType.Media},
			{ ".jpg", ScannedFileType.Media},
			{ ".jpeg", ScannedFileType.Media},
			{ ".xpm", ScannedFileType.Media},

			{ ".exe", ScannedFileType.PeBinary},
			{ ".dll", ScannedFileType.PeBinary},
			{ ".sys", ScannedFileType.PeBinary},
			{ ".com", ScannedFileType.PeBinary},

			{ ".lib", ScannedFileType.Library},
			{ ".a", ScannedFileType.Library},

			{ ".mak", ScannedFileType.BuildFile},
			{ ".sln", ScannedFileType.BuildFile},
			{ ".csproj", ScannedFileType.BuildFile},
			{ ".config", ScannedFileType.Configuration},

			{ ".txt", ScannedFileType.Document},
			{ ".xml", ScannedFileType.Document},
			{ ".xslt", ScannedFileType.Document},
			{ ".doc", ScannedFileType.Document},
			{ ".docx", ScannedFileType.Document},
			{ ".xls", ScannedFileType.Document},
			{ ".xlsx", ScannedFileType.Document},
			{ ".ppt", ScannedFileType.Document},
			{ ".pptx", ScannedFileType.Document},
			{ ".readme", ScannedFileType.Document}
		};
		#endregion --------------------------------------------------------------------------------------------------------------

	}
}
