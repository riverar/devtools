using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Scan.Types
{
	/// <summary>
	/// Defines the type of project files.
	/// </summary>
	public enum ScannedFileType
	{
		/// <summary>
		/// No type is known for this file
		/// </summary>
		Unknown,
		/// <summary>
		/// Generic source code file
		/// </summary>
		Source,
		/// <summary>
		/// C code file which may include header files
		/// </summary>
		C,
		/// <summary>
		/// Pascal source file
		/// </summary>
		Pascal,
		/// <summary>
		/// C# source file
		/// </summary>
		CSharp,
		/// <summary>
		/// Visual Basic source file
		/// </summary>
		VB,
		/// <summary>
		/// Assembly source file
		/// </summary>
		Assembly,
		/// <summary>
		/// Manifest file
		/// </summary>
		Manifest,
		/// <summary>
		/// Build file for compiling the project
		/// </summary>
		BuildFile,
		/// <summary>
		/// Script file for languages like perl, python, javascript, etc
		/// </summary>
		Script,
		/// <summary>
		/// Media files like images, videos, etc
		/// </summary>
		Media,
		/// <summary>
		/// Executable files like exe, dll, com
		/// </summary>
		PeBinary,
		/// <summary>
		/// Library files that may contain pre-compiled code
		/// </summary>
		Library,
		/// <summary>
		/// Any types of documentation files
		/// </summary>
		Document,
		/// <summary>
		/// Debug files like pdb
		/// </summary>
		Debug,
		/// <summary>
		/// Object code files which contain compiled code
		/// </summary>
		Object,
		/// <summary>
		/// Configuration files
		/// </summary>
		Configuration

	}
}
