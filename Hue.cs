using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Q42.HueApi;
using Q42.HueApi.Interfaces;

namespace zecil.AmbiHueTv
{
    public class HueColor
    {
        public int red = 0;
        public int green = 0;
        public int blue = 0;
    }

    public class Hue
    {
        public static string AppName = "AmbiHueTv";
        public static string DeviceName = "pi";

        #region Configurable settings
        /// <summary>
        /// Specifies the number of times to record color information per second.
        /// </summary>
        private const int RECORDSPERSECOND = 5;
        /// <summary>
        /// Number of seconds that color data is considered short-term
        /// </summary>
        private const int SHORTSECONDS = 6;
        /// <summary>
        /// Multiplier to determine how long color data is kept in long-term storage.
        /// SHORTSECONDS is multiplied by this value to determine the length of time.
        /// </summary>
        private const int LONGMULTIPLIER = 10;
        /// <summary>
        /// Weighting factor applied to short-term data when the short and long term values are averaged to determine the target color.
        /// </summary>
        private const double SHORTWEIGHT = 1;
        /// <summary>
        /// Weighting factor applied to long-term data when the short and long term values are averaged to determine the target color.
        /// </summary>
        private const double LONGWEIGHT = 1;
        /// <summary>
        /// Maximum change allowed in each color change per update interval.
        /// Small numbers create smoother transitions, but lights take longer to reach the target color.
        /// Large numbers cause lights to reach target faster, but transitions will be more obvious.
        /// </summary>
        private const int CHANGELIMIT = 3;
        /// <summary>
        /// Number of times to update the light color each second.
        /// </summary>
        private const int LIGHTUPDATESPERSECOND = 2;
        #endregion

        #region Non-configurable settings
        // These constants are simple calculations based on the configurable constants.
        // These should not generally be changed.
        private const int QUEUELENGTH = SHORTSECONDS * RECORDSPERSECOND;
        private const int RECORDINTERVAL = 1000 / RECORDSPERSECOND;
        private const int CHANGEINTERVAL = 1000 / LIGHTUPDATESPERSECOND; 
        #endregion

        private ObservableCollection<string> _bridgeIps;
        private IEnumerable<Light> _allLights;
        private ObservableCollection<Light> _filteredLights;
        private ILocalHueClient _client;

        #region Color Queues
        // These Queues contain the short-term color information
        private Queue<int> redQueue = new Queue<int>(QUEUELENGTH + 1);
        private Queue<int> blueQueue = new Queue<int>(QUEUELENGTH + 1);
        private Queue<int> greenQueue = new Queue<int>(QUEUELENGTH + 1);

        // These Queues contain the long-term color information
        private Queue<int> redLongQueue = new Queue<int>((QUEUELENGTH * LONGMULTIPLIER) + 1);
        private Queue<int> blueLongQueue = new Queue<int>((QUEUELENGTH * LONGMULTIPLIER) + 1);
        private Queue<int> greenLongQueue = new Queue<int>((QUEUELENGTH * LONGMULTIPLIER) + 1);
        #endregion

        #region Color Objects
        /// <summary>
        /// The target color based on the recorded color data.
        /// </summary>
        private HueColor target = new HueColor();
        /// <summary>
        /// The current color being sent to the hue lights
        /// </summary>
        private HueColor current = new HueColor();
        /// <summary>
        /// The most recent color provided by the frame analysis
        /// </summary>
        private HueColor recent = new HueColor();

        /// <summary>
        /// Locking object used when target colors or queues are updated
        /// </summary>
        private object targetUpdate = new object(); 
        #endregion

        #region Timer objects
        private Timer changeTimer;
        private Timer recordTimer;
        AutoResetEvent autoChangeEvent = new AutoResetEvent(false);
        AutoResetEvent autoRecordEvent = new AutoResetEvent(false);
        private bool changeTimerStarted = false;
        private bool recordTimerStarted = false; 
        #endregion

        public Hue()
        {
            // Initialize the timers used for the light change and color record tasks     
            changeTimer = new Timer(ChangeColorTask,autoChangeEvent,Timeout.Infinite,500);
            recordTimer = new Timer(RecordColorTask, autoRecordEvent, Timeout.Infinite, 200);
        }

        public async Task<string> FindFirstBridge()
        {
            _bridgeIps = new ObservableCollection<string>();
            IBridgeLocator locator = new HttpBridgeLocator();
            IEnumerable<string> bridgeIPs = await locator.LocateBridgesAsync(TimeSpan.FromSeconds(5));

            _bridgeIps.Clear();
            foreach (var ip in bridgeIPs)
            {
                _bridgeIps.Add(ip);
            }

            if (_bridgeIps.Count > 0)
            {
                return _bridgeIps[0];
            }

            return string.Empty;
        }

        public async Task RegisterIfNeeded()
        {
            _client = new LocalHueClient(Settings.DefaultBridgeIp);

            if (string.IsNullOrEmpty(Settings.TheAppKey))
            {
                Settings.TheAppKey = await _client.RegisterAsync(AppName, DeviceName);
            }
        }

        public async Task Initialize()
        {
            _client.Initialize(Settings.TheAppKey);
            _allLights = await _client.GetLightsAsync();

        }

        public void FilterLightsByNameContaining(string searchPhrase)
        {

            _filteredLights = new ObservableCollection<Light>();
            _filteredLights.Clear();
            foreach (var light in _allLights)
            {
                if (light.Name.Contains(searchPhrase))
                    _filteredLights.Add(light);
            }

        }

        public async Task TurnOnFilteredLights()
        {
            var modifiedLights = new List<string>(_filteredLights.Select(a => a.Id));
            var command = new LightCommand();

            command.TurnOn();
            command.Brightness = 200;
            await _client.SendCommandAsync(command, modifiedLights);

        }

        public async Task TurnOffFilteredLights()
        {
            var modifiedLights = new List<string>(_filteredLights.Select(a => a.Id));
            var command = new LightCommand();

            command.TurnOff();
            await _client.SendCommandAsync(command, modifiedLights);

        }

        /// <summary>
        /// Sets the most recent color data used by the color selection algorithm
        /// </summary>
        /// <param name="red">Red value, expressed as an integer from 0-254</param>
        /// <param name="green">Green value, expressed as an integer from 0-254</param>
        /// <param name="blue">Blue value, expressed as an integer from 0-254</param>
        public void UpdateColor(int red, int green, int blue)
        {
            /* 
             *  Assign the submitted data to the recent object.
             *  It will added to the color queues by the record timer.
             *  Using the timer allows data to be added to the queue at a 
             *  constant interval.
             */
            recent.red = red;
            recent.green = green;
            recent.blue = blue;

            // Start the record timer if it hasn't already been started.
            // This should only happen the first time that UpdateColor is called.
            if (!recordTimerStarted)
            {
                recordTimer.Change(0, RECORDINTERVAL);
                recordTimerStarted = true;
            }
#if DEBUG
            Debug.WriteLine("Red " + redQueue.Count.ToString() + " Green " + greenQueue.Count.ToString() + " Blue " + blueQueue.Count.ToString());
            Debug.WriteLine("Red " + current.red + "/" + target.red + "Blue " + current.blue + "/" + target.blue + "Green " + current.green + "/" + target.green);
#endif

        }

        /// <summary>
        /// Determines the most acceptable value for each light update
        /// </summary>
        /// <param name="currentValue"></param>
        /// <param name="targetValue"></param>
        /// <returns>The closest value to the target that can be set limited by CHANGELIMIT</returns>
        private static int adjustColor(int currentValue, int targetValue)
        {
            if (currentValue != targetValue)
            {
                if (currentValue > targetValue) currentValue -= Math.Min((currentValue - targetValue), CHANGELIMIT);
                if (targetValue > currentValue) currentValue += Math.Min((targetValue - currentValue), CHANGELIMIT);
            }
            return currentValue;
        }

        /// <summary>
        /// Get the weighted average of the provided queues based on the constant configuration settings.
        /// </summary>
        /// <param name="shortQueue">The short-term queue to use for the calculation</param>
        /// <param name="longQueue">The long-term queue to use for the calculation</param>
        /// <returns></returns>
        private static int getWeightedAverage(Queue<int> shortQueue, Queue<int> longQueue)
        {
            int weightedAverage;

            // Determine if there is data in the long queue.
            // If there is not, only use the unweighted short queue for the calculation.
            if(longQueue.Count > 0)
            {
                // Average the long and short queues with the appropriate weighting settings.
                weightedAverage = (int)(((shortQueue.Average() * SHORTWEIGHT) + (longQueue.Average() * LONGWEIGHT)) / (SHORTWEIGHT + LONGWEIGHT));
            } else
            {
                // There was no long-term data, Provide the average of the short queue
                weightedAverage = (int)(shortQueue.Average());
            }
            return weightedAverage;
        }

        /// <summary>
        /// Function called by the change timer that sets the current light color
        /// </summary>
        /// <param name="stateInfo"></param>
        private void ChangeColorTask(Object stateInfo)
        {
            // This lock prevents the queues from being updated until the target color is determined.
            lock (targetUpdate)
            {
                // Update the target color object with the weighted average for each color channel
                target.blue = getWeightedAverage(blueQueue, blueLongQueue);
                target.green = getWeightedAverage(greenQueue, greenLongQueue);
                target.red = getWeightedAverage(redQueue, redLongQueue);
            }

            // Determine the new color to send to the lights based on the target color and change limits
            current.blue = adjustColor(current.blue, target.blue);
            current.red = adjustColor(current.red, target.red);
            current.green = adjustColor(current.green, target.green);

            // Send the new color to the lights
            var modifiedLights = new List<string>(_filteredLights.Select(a => a.Id));
            var command = new LightCommand
            {
                TransitionTime = TimeSpan.FromMilliseconds(CHANGEINTERVAL)
            };
            command.SetColor(current.red, current.green, current.blue);
            SetColors(command, modifiedLights);
        }

        private void SetColors(LightCommand command,List<string> modifiedLights)
        {
            Task working = _client.SendCommandAsync(command, modifiedLights);
            working.Wait();
        }

        /// <summary>
        /// Shift old data from the short queue to the long queue and very old data out of the long queue
        /// </summary>
        /// <param name="shortQueue"></param>
        /// <param name="longQueue"></param>
        private static void shiftQueue(ref Queue<int> shortQueue, ref Queue<int> longQueue)
        {
            if (shortQueue.Count == QUEUELENGTH)
            {
                if (longQueue.Count == QUEUELENGTH*LONGMULTIPLIER)
                {
                    longQueue.Dequeue();
                }

                longQueue.Enqueue(shortQueue.Dequeue());
            }
        }

        /// <summary>
        /// Function called by the record timer. Updates the color queues with the most recent color data.
        /// </summary>
        /// <param name="stateInfo"></param>
        private void RecordColorTask(Object stateInfo)
        {
            // Lock the queues so that the average won't be taken while the queue is being updated
            lock (targetUpdate)
            {
                // Shift data from the short-term queues to the long-term queues
                shiftQueue(ref redQueue, ref redLongQueue);
                shiftQueue(ref blueQueue, ref blueLongQueue);
                shiftQueue(ref greenQueue, ref greenLongQueue);

                // Add the most recent data to the short-term queues
                redQueue.Enqueue(recent.red);
                greenQueue.Enqueue(recent.green);
                blueQueue.Enqueue(recent.blue);
            }
            
            // Start the change timer if it hasn't already been started.
            // This should only happen the first time the function is called
            if (!changeTimerStarted)
            {
                changeTimer.Change(0, CHANGEINTERVAL);
                changeTimerStarted = true;
            }
        }

    }
}
