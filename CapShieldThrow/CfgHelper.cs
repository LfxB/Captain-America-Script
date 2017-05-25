using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CfgHelper
{
    class CfgHelperClass
    {

        List<string> AllSettingsWithFullPath = new List<string>();

        public void LoadCustomSettings(string directory, string searchpattern)
        {
            string[] files = Directory.GetFiles(directory, searchpattern);

            AllSettingsWithFullPath.Clear();

            AllSettingsWithFullPath.AddRange(files);
        }

        public List<string> GetCleanFileNames()
        {
            List<string> temp = new List<string>();

            foreach (string s in AllSettingsWithFullPath)
            {
                string name = Path.GetFileNameWithoutExtension(s);
                temp.Add(name);
            }

            return temp;
        }
    }
}
