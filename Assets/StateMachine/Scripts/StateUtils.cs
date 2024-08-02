﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityObject = UnityEngine.Object;

namespace StateUtils {
    [Serializable]
    public class StateMachine<T> where T : Enum, IConvertible {
        private bool hasUnityObjectOwner;
        private UnityObject _owner;
        [SerializeField, Header("State")]
        private T _state;
        [SerializeField, Header("Previous State")]
        private T _prevState;
        [SerializeField, Header("Time since state changed")]
        private float _timeSinceStateChanged;

        [NonSerialized]
        private bool hasSubscribedToUpdate = false;

        [NonSerialized]
        private bool useFixedUpdateInstead = false;

        [NonSerialized]
        // If set to true, will use value not scaled by Time.timeScale
        private bool useRealTime = false;

        public delegate void OnStateChangeEvent(T prevState, float prevTimeSinceStateChanged);
        public delegate void OnStateChangeEventSimple();
        public event OnStateChangeEvent OnStateChange;
        public event OnStateChangeEventSimple OnStateChangeSimple;
        public Dictionary<T, UnityEvent> onStateChangeDict;

        [Serializable]
        public struct StateMachineUnityEvent {
            public T state;
            public UnityEvent onStateChange;
        }

        public List<StateMachineUnityEvent> onStateChange;

        public T state {
            get {
                InitIdempotent();
                return _state;
            }
            private set {
                InitIdempotent();
                if (value.Equals(_state)) {
                    return;
                }

                ForceSetState(value);
            }
        }

        private void ForceSetState(T newState) {
            float prevTimeSinceStateChanged = _timeSinceStateChanged;
            _timeSinceStateChanged = 0f;
            _prevState = _state;
            _state = newState;

            OnStateChange?.Invoke(prevState, prevTimeSinceStateChanged);
            if (onStateChangeDict.ContainsKey(newState)) {
                onStateChangeDict[newState]?.Invoke();
            }
            OnStateChangeSimple?.Invoke();
        }

        public T prevState => _prevState;

        public float timeSinceStateChanged {
            get {
                InitIdempotent();
                return _timeSinceStateChanged;
            }
            set {
                InitIdempotent();
                _timeSinceStateChanged = value;
            }
        }

        public static implicit operator T(StateMachine<T> stateMachine) => stateMachine.state;
        public static implicit operator int(StateMachine<T> stateMachine) => (int)(object)stateMachine.state;
        
#region Constructors
        // Hide this constructor behind a static method to make sure the user intends it
        /// <summary>
        /// Creates a StateMachine without a UnityObject owner. NOTE: It is up to YOU to make sure the events are cleaned up properly!
        /// </summary>
        /// <param name="startingState">Which state to begin in</param>
        /// <param name="useFixedUpdateInstead">Whether we should use FixedUpdate instead of Update</param>
        /// <param name="useRealTime">Whether we should use real time or in-game scaled time</param>
        /// <returns>A new StateMachine without an owner. Make sure you clean up its events explicitly!</returns>
        public static StateMachine<T> CreateWithoutOwner(T startingState, bool useFixedUpdateInstead = false, bool useRealTime = false) {
            return new StateMachine<T>(startingState, useFixedUpdateInstead, useRealTime);
        }
        private StateMachine(T startingState, bool useFixedUpdateInstead = false, bool useRealTime = false) {
            this.hasUnityObjectOwner = false;
            this._state = startingState;
            this._prevState = _state;
            this._timeSinceStateChanged = 0f;
            this.useFixedUpdateInstead = useFixedUpdateInstead;
            this.useRealTime = useRealTime;
            
            InitIdempotent();
        }
        
        /// <summary>
        /// Creates a new StateMachine instance with the given owner and starting state
        /// </summary>
        /// <param name="owner">Unity Object that this StateMachine is associated with</param>
        /// <param name="startingState">Which state to begin in</param>
        public StateMachine(UnityObject owner, T startingState) {
            this.hasUnityObjectOwner = true;
            this._owner = owner;
            this._state = startingState;
            this._prevState = _state;
            this._timeSinceStateChanged = 0f;
            
            InitIdempotent();
        }
        
        /// <summary>
        /// Creates a new StateMachine instance with the given owner, starting state, and configuration options
        /// </summary>
        /// <param name="owner">Unity Object that this StateMachine is associated with</param>
        /// <param name="startingState">Which state to begin in</param>
        /// <param name="useFixedUpdateInstead">Whether we should use FixedUpdate instead of Update</param>
        /// <param name="useRealTime">Whether we should use real time or in-game scaled time</param>
        public StateMachine(UnityObject owner, T startingState, bool useFixedUpdateInstead = false, bool useRealTime = false) {
            this.hasUnityObjectOwner = true;
            this._owner = owner;
            this._state = startingState;
            this._prevState = _state;
            this._timeSinceStateChanged = 0f;
            this.useFixedUpdateInstead = useFixedUpdateInstead;
            this.useRealTime = useRealTime;
            
            InitIdempotent();
        }

        private StateMachine() { }
#endregion

        /// <summary>
        /// Immediately change to the given state, and reset the time since state changed
        /// </summary>
        /// <param name="newState">Which state to immediately change to</param>
        public void Set(T newState) {
            state = newState;
        }

#region AddStateTransition
        /// <summary>
        /// Automatically transition between states at a given time
        /// </summary>
        /// <param name="fromState">State to be in before the transition</param>
        /// <param name="toState">State to be in after the transition</param>
        /// <param name="atTime">Time when state transition occurs</param>
        public void AddStateTransition(T fromState, T toState, float atTime) {
            TimedEventTrigger stateTransitionTrigger = new TimedEventTrigger() {
                forState = fromState,
                atTime = atTime
            };
            
            timedStateTransitions.Add(stateTransitionTrigger, () => state = toState);
        }

        /// <summary>
        /// Automatically transition between states when a given predicate is satisfied. Checks once per Update
        /// </summary>
        /// <param name="fromState">State to be in before the transition</param>
        /// <param name="toState">State to be in after the transition</param>
        /// <param name="triggerWhen">Predicate to be satisfied to transition state</param>
        public void AddStateTransition(T fromState, T toState, Func<bool> triggerWhen) {
            CustomEventTrigger customEventTrigger = new CustomEventTrigger() {
                forState = fromState,
                triggerWhen = triggerWhen
            };
            
            customStateTransitions.Add(customEventTrigger, () => state = toState);
        }

        /// <summary>
        /// Automatically transition between states at a given time.
        /// Target state is defined at transition time by a given function.
        /// </summary>
        /// <param name="fromState">State to be in before the transition</param>
        /// <param name="toStateDef">Function whose output is the state to be in after the transition</param>
        /// <param name="atTime">Time when state transition occurs</param>
        public void AddStateTransition(T fromState, Func<T> toStateDef, float atTime) {
            TimedEventTrigger stateTransitionTrigger = new TimedEventTrigger() {
                forState = fromState,
                atTime = atTime
            };
            
            timedStateTransitions.Add(stateTransitionTrigger, () => state = toStateDef.Invoke());
        }
        
        /// <summary>
        /// Automatically transition between states when a given predicate is satisfied.
        /// Checks once per Update, and target state is defined at transition time by a given function.
        /// </summary>
        /// <param name="fromState">State to be in before the transition</param>
        /// <param name="toStateDef">Function whose output is the state to be in after the transition</param>
        /// <param name="triggerWhen">Predicate to be satisfied to transition state</param>
        public void AddStateTransition(T fromState, Func<T> toStateDef, Func<bool> triggerWhen) {
            CustomEventTrigger customEventTrigger = new CustomEventTrigger() {
                forState = fromState,
                triggerWhen = triggerWhen
            };
            
            customStateTransitions.Add(customEventTrigger, () => state = toStateDef.Invoke());
        }
#endregion
#region AddTrigger
        /// <summary>
        /// Registers action to be triggered immediately upon switching to the given state
        /// </summary>
        /// <param name="forState">State to trigger the action in</param>
        /// <param name="whatToDo">Action to fire</param>
        public void AddTrigger(T forState, Action whatToDo) {
            AddTriggerImmediatelyForStates(s => s.Equals(forState), whatToDo);
        }
        
        /// <summary>
        /// Registers action to be triggered immediately upon switching to a state that satisfies the given predicate
        /// </summary>
        /// <param name="forStates">Predicate for current state to satisfy for triggering action</param>
        /// <param name="whatToDo">Action to fire</param>
        public void AddTrigger(Func<T, bool> forStates, Action whatToDo) {
            AddTriggerImmediatelyForStates(forStates, whatToDo);
        }
        
        /// <summary>
        /// Registers action to be triggered at a given time after switching to a given state
        /// </summary>
        /// <param name="forState">State to trigger the action in</param>
        /// <param name="atTime">Time after switching to target state to trigger action at</param>
        /// <param name="whatToDo">Action to fire</param>
        public void AddTrigger(T forState, float atTime, Action whatToDo) {
            TimedEventTrigger timedEventTrigger = new TimedEventTrigger { forState = forState, atTime = atTime };
            timedEvents.Add(timedEventTrigger, whatToDo);
        }
        
        /// <summary>
        /// Registers action to be triggered when a given predicate is satisfied for a given state. Checks once per Update
        /// </summary>
        /// <param name="forState">State to trigger the action in</param>
        /// <param name="triggerWhen">Predicate to be satisfied for the action to fire</param>
        /// <param name="whatToDo">Action to fire</param>
        public void AddTrigger(T forState, Func<bool> triggerWhen, Action whatToDo) {
            CustomEventTrigger customEventTrigger = new CustomEventTrigger { forState = forState, triggerWhen = triggerWhen };
            customEvents.Add(customEventTrigger, whatToDo);
        }

        /// <summary>
        /// Registers action to be triggered at a given time after switching to a state that satisfies the given predicate
        /// </summary>
        /// <param name="forStates">Predicate for current state to satisfy for triggering action</param>
        /// <param name="atTime">Time after switching to target state to trigger action at</param>
        /// <param name="whatToDo">Action to fire</param>
        public void AddTrigger(Func<T, bool> forStates, float atTime, Action whatToDo) {
            foreach (T enumValue in Enum.GetValues(typeof(T))) {
                if (forStates.Invoke(enumValue)) {
                    AddTrigger(enumValue, atTime, whatToDo);
                }
            }
        }
        
        /// <summary>
        /// Registers action to be triggered when the triggerWhen predicate is satisfied, for a state which satisfies the forStates predicate. Checks once per Update
        /// </summary>
        /// <param name="forStates">Predicate for current state to satisfy for triggering action</param>
        /// <param name="triggerWhen">Predicate to be satisfied for the action to fire</param>
        /// <param name="whatToDo">Action to fire</param>
        public void AddTrigger(Func<T, bool> forStates, Func<bool> triggerWhen, Action whatToDo) {
            foreach (T enumValue in Enum.GetValues(typeof(T))) {
                if (forStates.Invoke(enumValue)) {
                    AddTrigger(enumValue, triggerWhen, whatToDo);
                }
            }
        }
        
        private void AddTriggerImmediatelyForStates(Func<T, bool> forStates, Action whatToDo) {
            this.OnStateChangeSimple += () => {
                if (forStates(state)) {
                    whatToDo.Invoke();
                }
            };
        }
#endregion
#region WithUpdate
        /// <summary>
        /// Registers supplied action to run on every Update while in any state
        /// </summary>
        /// <param name="forTime">Action to run at a given time since state changed</param>
        public void WithUpdate(Action<float> forTime) {
            CustomUpdate customUpdate = new CustomUpdate { updateAction = forTime };
            updateForAllStates.Add(customUpdate);
        }
        
        /// <summary>
        /// Registers supplied action to run on every Update while in given state
        /// </summary>
        /// <param name="forState">State to be in when running the action</param>
        /// <param name="forTime">Action to run at a given time since state changed</param>
        public void WithUpdate(T forState, Action<float> forTime) {
            CustomUpdate customUpdate = new CustomUpdate { updateAction = forTime };
            List<CustomUpdate> customUpdatesForState = updateActions.TryGetValue(forState, out var updates)
                ? updates
                : new List<CustomUpdate>();
            customUpdatesForState.Add(customUpdate);
            updateActions[forState] = customUpdatesForState;
        }
#endregion
        
# region Custom Update

        class CustomUpdate {
            public Action<float> updateAction;
        }
        
        [NonSerialized]
        private Dictionary<T, List<CustomUpdate>> _updateActions;
        private Dictionary<T, List<CustomUpdate>> updateActions => _updateActions ??= new Dictionary<T, List<CustomUpdate>>();

        private List<CustomUpdate> _updateForAllStates;
        private List<CustomUpdate> updateForAllStates => _updateForAllStates ??= new List<CustomUpdate>();

#endregion
        
#region Custom Events

        class CustomEventTrigger {
            public T forState;
            public Func<bool> triggerWhen;
        }

        [NonSerialized]
        private Dictionary<CustomEventTrigger, Action> _customEvents;
        private Dictionary<CustomEventTrigger, Action> customEvents => _customEvents ??= new Dictionary<CustomEventTrigger, Action>();
        private HashSet<CustomEventTrigger> customEventsTriggeredInState = new HashSet<CustomEventTrigger>();

        class TimedEventTrigger {
            public T forState;
            public float atTime;
        }

        [NonSerialized] private Dictionary<TimedEventTrigger, Action> _timedEvents;
        private Dictionary<TimedEventTrigger, Action> timedEvents => _timedEvents ??= new Dictionary<TimedEventTrigger, Action>();

        [NonSerialized]
        private Dictionary<TimedEventTrigger, Action> _timedStateTransitions;
        private Dictionary<TimedEventTrigger, Action> timedStateTransitions => _timedStateTransitions ??= new Dictionary<TimedEventTrigger, Action>();

        [NonSerialized]
        private Dictionary<CustomEventTrigger, Action> _customStateTransitions;
        private Dictionary<CustomEventTrigger, Action> customStateTransitions => _customStateTransitions ??= new Dictionary<CustomEventTrigger, Action>();

        private void TriggerEvents(float prevTime) {
            // Don't trigger events while Time.deltaTime is 0
            if (Math.Abs(prevTime - _timeSinceStateChanged) < float.Epsilon) return;

            // Timed and Custom events are triggered before state transitions
            foreach (var triggerAndAction in timedEvents) {
                TimedEventTrigger trigger = triggerAndAction.Key;
                if (trigger.forState.Equals(_state) &&
                    trigger.atTime >= prevTime &&
                    trigger.atTime < _timeSinceStateChanged) {
                    triggerAndAction.Value.Invoke();
                }
            }
            foreach (var triggerAndAction in customEvents) {
                CustomEventTrigger trigger = triggerAndAction.Key;
                // Only trigger the event once per state
                if (customEventsTriggeredInState.Contains(trigger)) continue;
                
                if (trigger.forState.Equals(_state) && trigger.triggerWhen.Invoke()) {
                    customEventsTriggeredInState.Add(trigger);
                    triggerAndAction.Value.Invoke();
                }
            }
            
            // Timed and Custom state transitions are triggered after all other events
            foreach (var triggerAndAction in timedStateTransitions) {
                TimedEventTrigger trigger = triggerAndAction.Key;
                if (trigger.forState.Equals(_state) && trigger.atTime >= prevTime &&
                    trigger.atTime < _timeSinceStateChanged) {
                    triggerAndAction.Value.Invoke();
                }
            }
            foreach (var triggerAndAction in customStateTransitions) {
                CustomEventTrigger trigger = triggerAndAction.Key;
                if (trigger.forState.Equals(_state) && trigger.triggerWhen.Invoke()) {
                    triggerAndAction.Value.Invoke();
                }
            }
        }

        private void RunUpdateActions() {
            foreach (var update in updateForAllStates) {
                try {
                    update.updateAction.Invoke(timeSinceStateChanged);
                }
                catch (Exception e) {
                    Debug.LogError(_owner + " threw an exception: " + e.Message + "\n" + e.StackTrace);
                }
            }
            
            if (updateActions.ContainsKey(state)) {
                foreach (var update in updateActions[state]) {
                    try {
                        update.updateAction.Invoke(timeSinceStateChanged);
                    }
                    catch (Exception e) {
                        Debug.LogError(_owner + " threw an exception: " + e.Message + "\n" + e.StackTrace);
                    }
                }
            }
        }
#endregion

        public void CleanUp() {
            if (hasSubscribedToUpdate) {
                try {
                    GlobalUpdate.instance.UpdateGlobal -= Update;
                }
                catch {
                    // Sometimes this can throw errors when we clean up due to leaving play mode, we don't really care about these errors
                }
            }
        }

        private void InitIdempotent() {
            if (hasSubscribedToUpdate || GlobalUpdate.instance == null) {
                // If possible, attempt to start a coroutine to init once GlobalUpdate is available
                if (_owner != null) {
                    // Attempt to cast the _owner to MonoBehaviour
                    MonoBehaviour monoBehaviourOwner = _owner as MonoBehaviour;
                    if (monoBehaviourOwner != null && monoBehaviourOwner.gameObject.activeInHierarchy) {
                        monoBehaviourOwner.StartCoroutine(InitCoroutine());
                        return;
                    }
                }

                return;
            }
            
            DoInit();
        }

        private void DoInit() {
            if (hasSubscribedToUpdate || GlobalUpdate.instance == null) return;
            
            onStateChangeDict = onStateChange?.ToDictionary(unityEvent => unityEvent.state, unityEvent => unityEvent.onStateChange) ?? new Dictionary<T, UnityEvent>();

            if (useFixedUpdateInstead) {
                GlobalUpdate.instance.FixedUpdateGlobal += Update;
            }
            else {
                GlobalUpdate.instance.UpdateGlobal += Update;
            }
            hasSubscribedToUpdate = true;
        }

        IEnumerator InitCoroutine() {
            yield return new WaitWhile(() => GlobalUpdate.instance == null);
            DoInit();
        }

        // Does either Update or FixedUpdate based on config
        private void Update() {
            // If the Unity Object that was using this StateMachine is destroyed, cleanup event subscriptions
            if (hasUnityObjectOwner && _owner == null) {
                CleanUp();
                return;
            }
            
            float prevTime = _timeSinceStateChanged;

            float GetDeltaTime() {
                switch (useFixedUpdateInstead, useRealTime) {
                    case (true, true):
                        return Time.fixedUnscaledDeltaTime;
                    case (true, false):
                        return Time.fixedDeltaTime;
                    case (false, true):
                        return Time.unscaledDeltaTime;
                    case (false, false):
                        return Time.deltaTime;
                }
            }
            
            _timeSinceStateChanged += GetDeltaTime();
            
            TriggerEvents(prevTime);
            RunUpdateActions();
        }

#region Serialization
        /// <summary>
        /// Creates a serializable object containing the current state and time since state changed
        /// </summary>
        /// <returns>Serializable object containing the current state and time since state changed</returns>
        public StateMachineSave ToSave() {
            StateMachineSave save = new StateMachineSave {
                timeSinceStateChanged = timeSinceStateChanged,
                state = state
            };
            return save;
        }

        /// <summary>
        /// Loads the state and time since state changed from a StateMachineSave
        /// </summary>
        /// <param name="save">Serializable object containing the state and time since state changed to be loaded</param>
        public void LoadFromSave(StateMachineSave save) {
            this.InitIdempotent();
            this._state = save.state;
            this._timeSinceStateChanged = save.timeSinceStateChanged;
        }
#endregion
        
        [Serializable]
        // Simple serializable class containing the state and time since state changed, for saving and loading
        public class StateMachineSave {
            public float timeSinceStateChanged;
            public T state;
        }
    }

    public static class StateMachineExt {
        // Allows creation of StateMachine with a UnityObject owner, which ensures proper event cleanup when the owner is destroyed
        public static StateMachine<T> StateMachine<T>(this UnityObject owner, T startingState) where T : Enum {
            return new StateMachine<T>(owner, startingState);
        }

        public static StateMachine<T> StateMachine<T>(this UnityObject owner, T startingState, bool useFixedUpdateInstead = false, bool useRealTime = false) where T : Enum {
            return new StateMachine<T>(owner, startingState, useFixedUpdateInstead, useRealTime);
        }
    }
}
