using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CoApp.Developer.Toolkit.PortableExecutable {
    using CoApp.Toolkit.Exceptions;
    using CoApp.Toolkit.Extensions;
    using Microsoft.Cci;

    public class PEAnalyzer {

        /// <summary>
        /// Not Even Started.
        /// </summary>
        /// <param name="filename"></param>
        public static void Load(string filename) {
             MetadataReaderHost _host = new PeReader.DefaultHost();

             var module = _host.LoadUnitFrom(filename) as IModule;

             if (module == null || module is Dummy) {
                 throw new CoAppException("{0} is not a PE file containing a CLR module or assembly.".format(filename));
             }
             var ILOnly = module.ILOnly;

            Console.WriteLine("module: {0}", module);
        }
    }
}
