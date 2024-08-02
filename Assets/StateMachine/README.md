# Prerequisites
Make sure you have the GlobalUpdate prefab object placed into any active scene; it will take care of providing the StateMachine class with 
updates on every frame.



# The StateMachine Class

The StateMachine class is an abstraction for programming state machines in Unity that reduces boilerplate code and allows you to write 
declarative, high-level code that is easy and intuitive to read and understand, without having to worry about implementing the low-level 
logic to achieve this.

Features of the StateMachine class:
 - Write high-level, declarative code that describes the desired behavior of a given state without having to implement the low-level logic
 - Reduce boilerplate code needed to achieve state machine behavior in scripts
 - Allows for publish-subscribe interactions with OnStateChange events
 - Attaches to a UnityObject owner at creation, and will automatically clean itself up when the owner is destroyed



## Getting Started With the StateMachine Class

To create a StateMachine, first you must define an enum listing out the possible states that may exist for it.

Here is an example for a sliding door:

```C#
// Define the different states the door can be in
public enum DoorState {
    Closed,
    Opening,
    Open,
    Closing
}
```

Then, you can create an instance of the StateMachine class using this enum definition.

Continuing with our door example:

```C#
public StateMachine<DoorState> state;
```

A convenience method exists for creating an instance of StateMachine in a given starting state, with the current script as the owner:

```C#
// Convenience extension method that is valid on any Unity Object (i.e. any MonoBehaviour)
state = this.StateMachine(DoorState.Closed);
```

**GOTCHA**: The StateMachine extension method can only be used after the MonoBehaviour is instantiated (since before that, `this` is undefined), 
so unfortunately we cannot inline the instantiation with the declaration, and instead need to do it in Awake() or Start().

Great! Now we have an instance of the StateMachine class, starting in the desired state and ready to define behavior with!



## Defining State Transitions

We have a StateMachine instance that is in some given starting state, but now we need to define how it will transition from one state to 
the next. For example, if a door is opening, it will finish opening and transition into the Open state after some time has passed:

```C#
// Set up timed state transitions. Door will automatically go from Opening/Closing to Open/Closed respectively after OPEN_CLOSE_TIME
state.AddStateTransition(DoorState.Opening, DoorState.Open, OPEN_CLOSE_TIME);
state.AddStateTransition(DoorState.Closing, DoorState.Closed, OPEN_CLOSE_TIME);
```

We can also define state transitions through the use of predicates instead of timing:

```C#
// Set up state transitions based on a predicate. Door will start Closing/Opening depending on the doorButton
state.AddStateTransition(DoorState.Closed, DoorState.Opening, () => doorButton.IsPressed);
state.AddStateTransition(DoorState.Open, DoorState.Closing, () => !doorButton.IsPressed);
```

Alternatively, we could define StateMachines in an imperative programming way, using the Set() method to directly change the state anywhere in 
the code. This can be useful, for instance, when you want to change the state as part of a subscription to another object's event being published:

```C#
// React to the doorButton pressed event to either open or close the door
doorButton.OnPress += () => {
    switch (state.state) {
        case DoorState.Opening:
        case DoorState.Open:
            // Directly set the state to Closing
            state.Set(DoorState.Closing);
            break;
        case DoorState.Closing:
        case DoorState.Closed:
            // Directly set the state to Opening
            state.Set(DoorState.Opening);
            break;
    }
};
```

Note: There are some other overloads of the AddStateTransition method to allow for more dynamic code, such as when you won't know what 
state you want to transition into until later in runtime. A full list of the API is provided at the end of this README!



# Defining StateMachine Behavior

Now we have a working StateMachine that will automatically transition between states after some amount of time has passed since the last 
state change, or after a given predicate is satisfied. Now all we need to do is add some behavior that should happen in each of the states!

To add a trigger that will fire immediately upon entering a given state, use the AddTrigger() method:

```C#
// Add triggers that happen when a state is entered
state.AddTrigger(DoorState.Closed, () => SetDoorsOpenAmount(0f));
state.AddTrigger(DoorState.Open, () => SetDoorsOpenAmount(1f));
```

Just like with defining the state transitions, we can also add triggers that will fire after some amount of time has passed in the given 
state, or if a given predicate is satisfied while in a given state. For more information on these and other use cases, please see the full 
API documentation at the end of this README.

To add a function that should run on every update for a given state, use the WithUpdate() method:

```C#
// Define code that should run every frame for a given state. This will open/close the doors smoothly over time
state.WithUpdate(DoorState.Opening, time => SetDoorsOpenAmount(time / OPEN_CLOSE_TIME));
state.WithUpdate(DoorState.Closing, time => SetDoorsOpenAmount(1 - time / OPEN_CLOSE_TIME));
```

The `time` provided to the function always takes on the value of the time elapsed since the last state change (how long have we been in 
the current state).

That's all! Now we have a fully-defined state machine which will automatically transition from state to state at the appropriate time, and
which will have the defined behavior in each state! Notice that we didn't have to write any of the low-level code that checks the current state, 
time, or the provided predicates, we just delegate all that work to the StateMachine class to handle for us!

Thank you for taking the time to read this, I hope it was helpful :)



# Full API Documentation

### Constructors/Instantiators:
```C#
/// <summary>
/// Creates a new StateMachine instance with the given owner and starting state
/// </summary>
/// <param name="owner">Unity Object that this StateMachine is associated with</param>
/// <param name="startingState">Which state to begin in</param>
public StateMachine(UnityObject owner, T startingState)


/// <summary>
/// Creates a new StateMachine instance with the given owner, starting state, and configuration options
/// </summary>
/// <param name="owner">Unity Object that this StateMachine is associated with</param>
/// <param name="startingState">Which state to begin in</param>
/// <param name="useFixedUpdateInstead">Whether we should use FixedUpdate instead of Update</param>
/// <param name="useRealTime">Whether we should use real time or in-game scaled time</param>
public StateMachine(UnityObject owner, T startingState, bool useFixedUpdateInstead = false, bool useRealTime = false)
```

The following convenience extension methods are provided for any Unity Object (i.e. any MonoBehaviour) for simplifying the creation syntax:
```C#
public static class StateMachineExt {
    // Allows easy creation of StateMachine with a UnityObject owner, which ensures proper event cleanup when the owner is destroyed
    // Usage: StateMachine<EnumType> stateMachine = this.StateMachine<EnumType>(startingState);
    public static StateMachine<T> StateMachine<T>(this UnityObject owner, T startingState) where T : Enum

    public static StateMachine<T> StateMachine<T>(this UnityObject owner, T startingState, bool useFixedUpdateInstead = false, bool useRealTime = false) where T : Enum
}
```

If needed, you can also create a StateMachine which is not attached to any Unity Object. Make sure you call the Cleanup() method when you no longer need it!
```C#
/// <summary>
/// Creates a StateMachine without a UnityObject owner. NOTE: It is up to YOU to make sure the events are cleaned up properly!
/// </summary>
/// <param name="startingState">Which state to begin in</param>
/// <param name="useFixedUpdateInstead">Whether we should use FixedUpdate instead of Update</param>
/// <param name="useRealTime">Whether we should use real time or in-game scaled time</param>
/// <returns>A new StateMachine without an owner. Make sure you clean up its events explicitly!</returns>
public static StateMachine<T> CreateWithoutOwner(T startingState, bool useFixedUpdateInstead = false, bool useRealTime = false)
```


### Defining State Transitions:
```C#
/// <summary>
/// Automatically transition between states at a given time
/// </summary>
/// <param name="fromState">State to be in before the transition</param>
/// <param name="toState">State to be in after the transition</param>
/// <param name="atTime">Time when state transition occurs</param>
public void AddStateTransition(T fromState, T toState, float atTime)

/// <summary>
/// Automatically transition between states when a given predicate is satisfied. Checks once per Update
/// </summary>
/// <param name="fromState">State to be in before the transition</param>
/// <param name="toState">State to be in after the transition</param>
/// <param name="triggerWhen">Predicate to be satisfied to transition state</param>
public void AddStateTransition(T fromState, T toState, Func<bool> triggerWhen)
```

Or the more advanced version which allows the desired state to be specified at transition time:
```C#
/// <summary>
/// Automatically transition between states at a given time.
/// Target state is defined at transition time by a given function.
/// </summary>
/// <param name="fromState">State to be in before the transition</param>
/// <param name="toStateDef">Function whose output is the state to be in after the transition</param>
/// <param name="atTime">Time when state transition occurs</param>
public void AddStateTransition(T fromState, Func<T> toStateDef, float atTime)

/// <summary>
/// Automatically transition between states when a given predicate is satisfied.
/// Checks once per Update, and target state is defined at transition time by a given function.
/// </summary>
/// <param name="fromState">State to be in before the transition</param>
/// <param name="toStateDef">Function whose output is the state to be in after the transition</param>
/// <param name="triggerWhen">Predicate to be satisfied to transition state</param>
public void AddStateTransition(T fromState, Func<T> toStateDef, Func<bool> triggerWhen)
```


### Explicitly Setting the State:
```C#
/// <summary>
/// Immediately change to the given state, and reset the time since state changed
/// </summary>
/// <param name="newState">Which state to immediately change to</param>
public void Set(T newState)
```

### Defining One-Time Behavior:
```C#
/// <summary>
/// Registers action to be triggered immediately upon switching to the given state
/// </summary>
/// <param name="forState">State to trigger the action in</param>
/// <param name="whatToDo">Action to fire</param>
public void AddTrigger(T forState, Action whatToDo)

/// <summary>
/// Registers action to be triggered immediately upon switching to a state that satisfies the given predicate
/// </summary>
/// <param name="forStates">Predicate for current state to satisfy for triggering action</param>
/// <param name="whatToDo">Action to fire</param>
public void AddTrigger(Func<T, bool> forStates, Action whatToDo)

/// <summary>
/// Registers action to be triggered at a given time after switching to a given state
/// </summary>
/// <param name="forState">State to trigger the action in</param>
/// <param name="atTime">Time after switching to target state to trigger action at</param>
/// <param name="whatToDo">Action to fire</param>
public void AddTrigger(T forState, float atTime, Action whatToDo)

/// <summary>
/// Registers action to be triggered when a given predicate is satisfied for a given state. Checks once per Update
/// </summary>
/// <param name="forState">State to trigger the action in</param>
/// <param name="triggerWhen">Predicate to be satisfied for the action to fire</param>
/// <param name="whatToDo">Action to fire</param>
public void AddTrigger(T forState, Func<bool> triggerWhen, Action whatToDo)

/// <summary>
/// Registers action to be triggered at a given time after switching to a state that satisfies the given predicate
/// </summary>
/// <param name="forStates">Predicate for current state to satisfy for triggering action</param>
/// <param name="atTime">Time after switching to target state to trigger action at</param>
/// <param name="whatToDo">Action to fire</param>
public void AddTrigger(Func<T, bool> forStates, float atTime, Action whatToDo)

/// <summary>
/// Registers action to be triggered when the triggerWhen predicate is satisfied, for a state which satisfies the forStates predicate. Checks once per Update
/// </summary>
/// <param name="forStates">Predicate for current state to satisfy for triggering action</param>
/// <param name="triggerWhen">Predicate to be satisfied for the action to fire</param>
/// <param name="whatToDo">Action to fire</param>
public void AddTrigger(Func<T, bool> forStates, Func<bool> triggerWhen, Action whatToDo)
```

### Defining Every-Frame Behavior:
```C#
/// <summary>
/// Registers supplied action to run on every Update while in given state
/// </summary>
/// <param name="forState">State to be in when running the action</param>
/// <param name="forTime">Action to run at a given time since state changed</param>
public void WithUpdate(T forState, Action<float> forTime)

/// <summary>
/// Registers supplied action to run on every Update while in any state
/// </summary>
/// <param name="forTime">Action to run at a given time since state changed</param>
public void WithUpdate(Action<float> forTime)
```

### Serialization (e.g. for Save/Load Systems):
```C#
[Serializable]
// Simple serializable class containing the state and time since state changed, for saving and loading
public class StateMachineSave {
    public float timeSinceStateChanged;
    public T state;
}

/// <summary>
/// Creates a serializable object containing the current state and time since state changed
/// </summary>
/// <returns>Serializable object containing the current state and time since state changed</returns>
public StateMachineSave ToSave()

/// <summary>
/// Loads the state and time since state changed from a StateMachineSave
/// </summary>
/// <param name="save">Serializable object containing the state and time since state changed to be loaded</param>
public void LoadFromSave(StateMachineSave save)
```
