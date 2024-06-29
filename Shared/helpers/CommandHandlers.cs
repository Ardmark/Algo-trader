using System;
using System.Windows.Input;

namespace AlgoTrader.Shared
{
    public class CommandHandlerBase
    {
        public bool _canExecute;// private Func<bool> _canExecute; - can pass a callback in like this

        /// <summary>
        /// Wires CanExecuteChanged event 
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        /// <summary>
        /// Forcess checking if execute is allowed
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute;
        }
    }
    public class CommandHandler : CommandHandlerBase, ICommand
    {
        private Action<object> _action;
        private object v1;
        private bool v2;

        public CommandHandler(Action<object> action, bool canExecute)
        {
            _action = action;
            _canExecute = canExecute;
        }

        public CommandHandler(object v1, bool v2)
        {
            this.v1 = v1;
            this.v2 = v2;
        }

        public void Execute(object parameter)
        {
            _action(parameter);
        }
    }

    public class CommandHandler1 : CommandHandlerBase, ICommand //as above, but without a command parameter (so the action, which is the method to be executed, has no parameter)
    {
        private Action _action;
        public CommandHandler1(Action action, bool canExecute)
        {
            _action = action;
            _canExecute = canExecute;
        }
        public void Execute(object parameter)
        {
            _action();
        }
    }
}
