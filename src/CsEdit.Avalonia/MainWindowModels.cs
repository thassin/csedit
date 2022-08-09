
using System.Collections.ObjectModel;

namespace CsEdit.Avalonia
{


    public class TreeItemProject
    {
        public string ProjectName { get; set; }

        public ObservableCollection<TreeItemFile> Files { get; }

        public TreeItemProject() {
            Files = new ObservableCollection<TreeItemFile>();
        }
    }


    public class TreeItemFile
    {
        public string FileName { get; set; }

        //public static int FileNameColumnWidth { get; set; }
        //public string ButtonText { get; set; }


    }


}

