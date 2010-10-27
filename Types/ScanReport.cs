using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace CoApp.Scan.Types
{
	/// <summary>
	/// Contains all information about the scanned project.
	/// </summary>
	[XmlRoot("report")]
	public class ScanReport
	{
		#region Private Fields --------------------------------------------------------------------------------------------------
		private Dictionary<int, ScannedFile> _fileHash = new Dictionary<int, ScannedFile>();
		private Dictionary<string, ScannedDefine> _defineHash = new Dictionary<string, ScannedDefine>();
		#endregion --------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Contains an array of files found.  This is used ONLY for serialization.
		/// </summary>
		/// <value>The files array.</value>
		[XmlArray("files"), XmlArrayItem("file", typeof(ScannedFile))]
		public ScannedFile[] FilesArray
		{
			get
			{
				return _fileHash.Values.ToArray<ScannedFile>();
			}
			set
			{
				_fileHash = new Dictionary<int, ScannedFile>();
				foreach (ScannedFile f in value)
					_fileHash[f.ID] = f;
			}
		}

		/// <summary>
		/// Contains an array of defines found. This is used ONLY for serialization.
		/// </summary>
		/// <value>The defines array.</value>
		[XmlArray("defines"), XmlArrayItem("define", typeof(ScannedDefine))]
		public ScannedDefine[] DefinesArray
		{
			get
			{
				ScannedDefine[] a = _defineHash.Values.ToArray<ScannedDefine>();
				Array.Sort<ScannedDefine>(a);
				return a;
			}
			set
			{
				_defineHash = new Dictionary<string, ScannedDefine>();
				foreach (ScannedDefine d in value)
					_defineHash[d.Name] = d;
			}
		}

		/// <summary>
		/// Provides access to the Defines in the report.  Each is keyed by its name.
		/// </summary>
		/// <value>The defines.</value>
		[XmlIgnore]
		public Dictionary<string, ScannedDefine> Defines
		{
			get { return _defineHash; }
			set { _defineHash = value; }
		}

		/// <summary>
		/// Provides access to the Files in the report.  Each is keyed by its numeric ID.
		/// </summary>
		/// <value>The files.</value>
		[XmlIgnore]
		public Dictionary<int, ScannedFile> Files
		{
			get { return _fileHash; }
			set { _fileHash = value; }
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ScanReport"/> class.
		/// </summary>
		public ScanReport()
		{
		}

		/// <summary>
		/// Finds the file with the given ID. 
		/// </summary>
		/// <param name="id">The id.</param>
		/// <returns>The ScanFile object or null if not found.</returns>
		public ScannedFile FindFile(int id)
		{
			if (_fileHash.ContainsKey(id))
				return _fileHash[id];
			return null;
		}

		/// <summary>
		/// Adds the given ScanFile instance to the Files dictionary.  This method should 
		/// be used because it assigns the numeric ID to the file if not present.
		/// </summary>
		/// <param name="file">The file.</param>
		public void AddFile(ScannedFile file)
		{
			if (file.ID == 0)
				file.ID = _fileHash.Count + 1;

			_fileHash[file.ID] = file;
		}

		/// <summary>
		/// Adds a define instance to the Defines dictionary.  
		/// </summary>
		/// <param name="identifier">The identifier/name of the define.</param>
		/// <param name="foundInFileId">The file id it was found in.</param>
		public void AddDefine(string identifier, int foundInFileId)
		{
			ScannedDefine define = FindDefine(identifier);

			if (define == null)
			{
				define = new ScannedDefine();
				define.Name = identifier;
				_defineHash[define.Name] = define;
			}

			define.UsedIn[foundInFileId] = 1;
		}

		/// <summary>
		/// Adds the given ScanDefine instance to the Defines hash if it does not already exist.
		/// </summary>
		/// <param name="define">The define.</param>
		public void AddDefine(ScannedDefine define)
		{
			if (_defineHash.ContainsKey(define.Name) == false)
			{
				_defineHash[define.Name] = define;
			}
		}

		/// <summary>
		/// Adds the define instance and value to the Defines dictionary.  Each define allows
		/// for multiple values and multiple files they are found in. This method adds any
		/// new information to currently stored records.
		/// </summary>
		/// <param name="identifier">The identifier/name of the define.</param>
		/// <param name="value">The value in the define.</param>
		/// <param name="foundInFileId">The file id it was found in.</param>
		public void AddDefineValue(string identifier, string value, int foundInFileId)
		{
			ScannedDefine define = FindDefine(identifier);

			if (define == null)
			{
				define = new ScannedDefine();
				define.Name = identifier;
				_defineHash[define.Name] = define;
			}

			define.Values[value] = 1;
			define.UsedIn[foundInFileId] = 1;
		}

		/// <summary>
		/// Finds the define given a name.
		/// </summary>
		/// <param name="define">The define.</param>
		/// <returns>The ScanDefine instance or null if not found.</returns>
		public ScannedDefine FindDefine(string define)
		{
			if (_defineHash.ContainsKey(define))
				return _defineHash[define];

			return null;
		}

		/// <summary>
		/// Takes an XML string and tries to return an instance of the class.
		/// </summary>
		/// <param name="xml">The XML source to deserialize.</param>
		/// <returns>An instance of the object of type ScanReport.</returns>
		public static ScanReport Deserialize(string xml)
		{
			if (string.IsNullOrEmpty(xml))
			{
				throw new ArgumentNullException("xml");
			}

			XmlSerializer serializer = new XmlSerializer(typeof(ScanReport));
			using (StringReader stream = new StringReader(xml))
			{
				try
				{
					return (ScanReport)serializer.Deserialize(stream);
				}
				catch (Exception ex)
				{
					// The serialization error messages are cryptic at best.
					// Give a hint at what happened
					if (ex.InnerException != null)
						throw new InvalidOperationException("Failed to " +
									 "create object from xml string", ex.InnerException);
					else
						throw new InvalidOperationException("Failed to " +
										 "create object from xml string", ex);

				}
			}
		}

		/// <summary>
		/// Returns an XML serialized version of the instance.
		/// </summary>
		/// <returns>A string containing the XML output.</returns>
		public string Serialize()
		{
			XmlSerializer serializer = new XmlSerializer(typeof(ScanReport));
			XmlSerializerNamespaces names = new XmlSerializerNamespaces();
			names.Add("", "");

			using (StringWriter stream = new StringWriter())
			{
				serializer.Serialize(stream, this, names);
				stream.Flush();
				return stream.ToString();
			}
		}


	}
}
