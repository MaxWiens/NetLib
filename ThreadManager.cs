using System;
using System.Collections.Generic;

namespace NetLib {

	public class ThreadManager {
		private readonly List<Action> _mainThreadBuffer = new List<Action>();
		private readonly List<Action> _mainThreadActions = new List<Action>();

		private bool _actionInBuffer = false;

		public void Update() {
			if(_actionInBuffer){
				_mainThreadActions.Clear();
				lock(_mainThreadBuffer){
					_mainThreadActions.AddRange(_mainThreadBuffer);
					_mainThreadBuffer.Clear();
				}
				foreach(Action a in _mainThreadActions) a();
			}
		}

		public void ExecuteOnMainThread(Action action){
			if(action != null){
				lock(_mainThreadBuffer){
					_mainThreadBuffer.Add(action);
					_actionInBuffer = true;
				}
			}
		}
	}
}
