using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace CoApp.Scan.Types
{
	/// <summary>
	/// Contains information about a "define" found in the project.
	/// </summary>
	[XmlRoot("define")]
	public class ScannedDefine : IComparable<ScannedDefine>
	{		
		#region Private Fields --------------------------------------------------------------------------------------------------
		private Dictionary<int, int> _usedIn = new Dictionary<int, int>();
		private Dictionary<string, int> _values = new Dictionary<string, int>();
		#endregion --------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Gets or sets the name of the define.
		/// </summary>
		/// <value>The name.</value>
		[XmlAttribute("name")]
		public string Name { get; set; }

		/// <summary>
		/// Used for serialization only.
		/// </summary>
		/// <value>The values array.</value>
		[XmlArray("values"),XmlArrayItem("value",typeof(string))]
		public string[] ValuesArray { 
			get {
				return _values.Keys.ToArray<string>();
			}
			set {
				_values = new Dictionary<string,int>();
				foreach (string s in value) 
					_values[s] = 1;
			}
		}

		/// <summary>
		/// Used for serialization only.
		/// </summary>
		/// <value>The used in array.</value>
		[XmlArray("usedin"), XmlArrayItem("fileid", typeof(int))]
		public int[] UsedInArray
		{
			get
			{
				int[] v = _usedIn.Keys.ToArray<int>();
				return v;
			}
			set
			{
				_usedIn = new Dictionary<int,int>();
				foreach (int i in value)
					_usedIn[i] = 1;
			}
		}

		/// <summary>
		/// Contains a dictionary of all file ID's that the define was found in.  The file ID's are stored as keys, the 'value' should always be 1.
		/// </summary>
		/// <value>The used in.</value>
		[XmlIgnore]
		public Dictionary<int, int> UsedIn
		{
			get { return _usedIn; }
			set { _usedIn = value; }
		}

		/// <summary>
		/// Contains a dictionary of all values that the define is used as.  The define values are stored as keys, the 'value' should always be 1.
		/// </summary>
		/// <value>The values.</value>
		[XmlIgnore]
		public Dictionary<string, int> Values
		{
			get { return _values; }
			set { _values = value; }
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ScannedDefine"/> class.
		/// </summary>
		public ScannedDefine()
		{
		}

		/// <summary>
		/// Compares one ScanDefine to another by comparing the Name in a case-sensitive manner.
		/// </summary>
		/// <param name="other">The other ScanDefine to compare to.</param>
		/// <returns>-1 if less, 0 if equal, +1 if greater</returns>
		public int CompareTo(ScannedDefine other)
		{
			return string.Compare(Name, other.Name, false);
		}

	}
}
