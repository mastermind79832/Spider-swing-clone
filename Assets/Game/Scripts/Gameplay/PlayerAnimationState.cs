namespace SpiderSwing.Gameplay
{
    /// <summary>
    /// Stable presentation state values shared by Unity and the Colyseus room.
    /// Keep the numeric order in sync with the server allow-list.
    /// </summary>
    public enum PlayerAnimationState
    {
        Idle = 0,
        Walk = 1,
        Jump = 2,
        SwingBack = 3,
        SwingForward = 4,
        Landing = 5,
    }
}
