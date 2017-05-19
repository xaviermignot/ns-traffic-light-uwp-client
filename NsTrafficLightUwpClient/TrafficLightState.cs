namespace NsTrafficLightUwpClient
{
    /// <summary>
    /// Contains all the possible states of a traffic light
    /// </summary>
    public enum TrafficLightState
    {
        /// <summary>
        /// Default value: all the lights are off
        /// </summary>
        Off,
        /// <summary>
        /// The green light is on
        /// </summary>
        Green,
        /// <summary>
        /// The orange (amber) light is on
        /// </summary>
        Orange,
        /// <summary>
        /// The red light is on
        /// </summary>
        Red,
        /// <summary>
        /// The light is broken
        /// </summary>        
        Broken
    }
}
