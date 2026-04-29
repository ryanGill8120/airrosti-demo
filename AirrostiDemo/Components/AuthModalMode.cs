namespace AirrostiDemo.Components;

/// <summary>
/// Controls which tab the shared <c>AuthModal</c> opens on. The modal is
/// reused from multiple call sites (the nav bar, the Drug Search page) and
/// each one wants the user to land on the right form by default — sign-up
/// from the "save my results" flow, log-in from the nav bar's "Sign in"
/// button.
/// </summary>
public enum AuthModalMode
{
    /// <summary>Open the modal showing the registration form first.</summary>
    Register,

    /// <summary>Open the modal showing the login form first.</summary>
    Login,
}
