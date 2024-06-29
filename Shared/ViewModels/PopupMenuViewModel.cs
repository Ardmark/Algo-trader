using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AlgoTrader.Shared.ViewModels
{
    public class PopupMenuViewModel
    {
        const int MAX_ITEMS = 19; //must set this to the number of ICommands included below

        //these don't all have to be used
        public ICommand MenuItem0_Command => menuItem_Commands[0];
        public ICommand MenuItem1_Command => menuItem_Commands[1];
        public ICommand MenuItem2_Command => menuItem_Commands[2];
        public ICommand MenuItem3_Command => menuItem_Commands[3];
        public ICommand MenuItem4_Command => menuItem_Commands[4];
        public ICommand MenuItem5_Command => menuItem_Commands[5];
        public ICommand MenuItem6_Command => menuItem_Commands[6];
        public ICommand MenuItem7_Command => menuItem_Commands[7];
        public ICommand MenuItem8_Command => menuItem_Commands[8];
        public ICommand MenuItem9_Command => menuItem_Commands[9];
        public ICommand MenuItem10_Command => menuItem_Commands[10];
        public ICommand MenuItem11_Command => menuItem_Commands[11];
        public ICommand MenuItem12_Command => menuItem_Commands[12];
        public ICommand MenuItem13_Command => menuItem_Commands[13];
        public ICommand MenuItem14_Command => menuItem_Commands[14];
        public ICommand MenuItem15_Command => menuItem_Commands[15];
        public ICommand MenuItem16_Command => menuItem_Commands[16];
        public ICommand MenuItem17_Command => menuItem_Commands[17];
        public ICommand MenuItem18_Command => menuItem_Commands[18];
        public ICommand MenuItem19_Command => menuItem_Commands[19];
        readonly List<ICommand> menuItem_Commands = new List<ICommand>();


        #region INotifyPropertyChanged implementation
        Type Type;
        public event PropertyChangedEventHandler PropertyChanged;
        protected void SetProperty<T>(string propName, T value)
        {
            try
            {
                if (Type == null)
                {
                    Type = GetType();
                }

                Type.GetProperty(propName).SetValue(this, value);
                OnPropertyChanged(propName);
            }
            catch { }
        }


        protected void SetProperty<T>(ref T store, T value, string propName, Action onChanged = null)
        {
            if (EqualityComparer<T>.Default.Equals(store, value))
                return;
            store = value;
            onChanged?.Invoke();
            OnPropertyChanged(propName);
        }


        //rw - this is thread safe
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion
        public PopupMenuViewModel(params Action[] actions)
        {      
            for (int i=0;i<MAX_ITEMS;i++)
            {
                menuItem_Commands.Add(actions.Length > i ? new CommandHandler1(actions[i], true) : null);
            }
        }
    }
}
