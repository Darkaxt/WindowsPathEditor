using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace WindowsPathEditor
{
    public class SelectablePath : INotifyPropertyChanged
    {
        private bool isSelected;

        public SelectablePath(string path, bool selected)
        {
            Path = path;
            isSelected = selected;
        }

        public string Path { get; private set; }
        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (isSelected == value) return;
                isSelected = value;

                var changed = PropertyChanged;
                if (changed != null)
                {
                    changed(this, new PropertyChangedEventArgs("IsSelected"));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
