// Note: this script has to be on an always-active UI parent, so that we can
// always find it from other code. (GameObject.Find doesn't find inactive ones)
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public partial class UILogin : MonoBehaviour
{
    public UIPopup uiPopup;
    public NetworkManagerMMO manager; // singleton=null in Start/Awake
    public NetworkAuthenticatorMMO auth;
    public GameObject panel;
    public Text statusText;
    public InputField accountInput;
    public InputField passwordInput;
    public Dropdown serverDropdown;
    public Button loginButton;
    public Button registerButton;
    public Toggle rememberMeToggle; // Added Remember Me Toggle
    [TextArea(1, 30)] public string registerMessage = "First time? Just log in and we will\ncreate an account automatically.";
    public Button hostButton;
    public Button dedicatedButton;
    public Button cancelButton;
    public Button quitButton;

    void Start()
    {
        // load last server by name in case order changes some day.
        if (PlayerPrefs.HasKey("LastServer"))
        {
            string last = PlayerPrefs.GetString("LastServer", "");
            serverDropdown.value = manager.serverList.FindIndex(s => s.name == last);
        }
        // Load saved account details if Remember Me was checked
        if (PlayerPrefs.HasKey("RememberMe") && PlayerPrefs.GetInt("RememberMe") == 1)
        {
            accountInput.text = PlayerPrefs.GetString("SavedAccount", "");
            passwordInput.text = PlayerPrefs.GetString("SavedPassword", "");
            rememberMeToggle.isOn = true;
        }
    }

    void OnDestroy()
    {
        // Save last server by name in case order changes some day
        PlayerPrefs.SetString("LastServer", serverDropdown.captionText.text);

        // Save account and password if Remember Me is checked
        if (rememberMeToggle.isOn)
        {
            PlayerPrefs.SetString("SavedAccount", accountInput.text);
            PlayerPrefs.SetString("SavedPassword", passwordInput.text);
            PlayerPrefs.SetInt("RememberMe", 1);
        }
        else
        {
            // Clear saved data if Remember Me is unchecked
            PlayerPrefs.DeleteKey("SavedAccount");
            PlayerPrefs.DeleteKey("SavedPassword");
            PlayerPrefs.SetInt("RememberMe", 0);
        }
    }

    void Update()
    {
        // Only show while offline
        // AND while in handshake since we don't want to show nothing while
        // trying to login and waiting for the server's response
        if (manager.state == NetworkState.Offline || manager.state == NetworkState.Handshake)
        {
            panel.SetActive(true);

            // Status
            if (NetworkClient.isConnecting)
                statusText.text = "Connecting...";
            else if (manager.state == NetworkState.Handshake)
                statusText.text = "Handshake...";
            else
                statusText.text = "";

            // Buttons. Interactable while network is not active
            registerButton.interactable = !manager.isNetworkActive;
            registerButton.onClick.SetListener(() => { uiPopup.Show(registerMessage); });
            loginButton.interactable = !manager.isNetworkActive && auth.IsAllowedAccountName(accountInput.text);
            loginButton.onClick.SetListener(() => { manager.StartClient(); });
            hostButton.interactable = Application.platform != RuntimePlatform.WebGLPlayer && !manager.isNetworkActive && auth.IsAllowedAccountName(accountInput.text);
            hostButton.onClick.SetListener(() => { manager.StartHost(); });
            cancelButton.gameObject.SetActive(NetworkClient.isConnecting);
            cancelButton.onClick.SetListener(() => { manager.StopClient(); });
            dedicatedButton.interactable = Application.platform != RuntimePlatform.WebGLPlayer && !manager.isNetworkActive;
            dedicatedButton.onClick.SetListener(() => { manager.StartServer(); });
            quitButton.onClick.SetListener(() => { NetworkManagerMMO.Quit(); });

            // Inputs
            auth.loginAccount = accountInput.text;
            auth.loginPassword = passwordInput.text;

            // Copy servers to dropdown; copy selected one to NetworkManager IP/Port
            serverDropdown.interactable = !manager.isNetworkActive;
            serverDropdown.options = manager.serverList.Select(
                sv => new Dropdown.OptionData(sv.name)
            ).ToList();
            manager.networkAddress = manager.serverList[serverDropdown.value].ip;
        }
        else panel.SetActive(false);
    }
}
