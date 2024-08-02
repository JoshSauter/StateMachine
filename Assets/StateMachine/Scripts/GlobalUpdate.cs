using UnityEngine;

// GlobalUpdate just provides an Update() call to anyone who wants it, including non-Monobehaviour classes
public class GlobalUpdate : MonoBehaviour {
    static GlobalUpdate _instance = null;

    public static GlobalUpdate instance {
        get {
            // If the singleton reference doesn't yet exist
            if (_instance == null) {
                // Search for a matching singleton that exists
                var matches = FindObjectsOfType<GlobalUpdate>();

                if (matches.Length > 0) {
                    _instance = matches[0];
                    if (matches.Length > 1) {
                        Debug.LogError("There is more than one GlobalUpdate in the scene.");
                    }
                }

                if (_instance == null) {
                    Debug.LogError("No GlobalUpdate exists. Make sure you add one to the scene. A prefab is available in the Prefabs folder.");
                }
            }

            return _instance;
        }
    }
    
    public delegate void GlobalUpdateEvent();
    public event GlobalUpdateEvent UpdateGlobal;
    public event GlobalUpdateEvent LateUpdateGlobal;
    public event GlobalUpdateEvent FixedUpdateGlobal;

    void Awake() {
        DontDestroyOnLoad(gameObject);
    }
    
    void Update() => UpdateGlobal?.Invoke();

    private void LateUpdate() => LateUpdateGlobal?.Invoke();

    private void FixedUpdate() => FixedUpdateGlobal?.Invoke();
}
