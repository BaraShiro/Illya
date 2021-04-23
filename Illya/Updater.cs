/*
    File:       Updater.cs
    Version:    1.0.0
    Author:     Robert Rosborg
 
 */

#nullable enable
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Illya
{
    /// <summary>
    /// A class for reading now playing information from MPC-HC's web interface and update the UI accordingly.
    /// </summary>
    public class Updater
    {
        /// <summary>The text block that displays the time.</summary>
        private readonly System.Windows.Controls.TextBlock _timeTextBlock;
        /// <summary>The text block that displays the name of the video file being played.</summary>
        private readonly System.Windows.Controls.TextBlock _videoNameTextBlock;
        /// <summary>The text block that displays the current and total playtime.</summary>
        private readonly System.Windows.Controls.TextBlock _playtimeTextBlock;
        /// <summary>The progressbar that displays the current playtime.</summary>
        private readonly System.Windows.Controls.ProgressBar _playtimeBar;

        /// <summary>The html code of the entire Variables.html page from MPC-HC's web interface,
        /// from which the now playing variables will be extracted.</summary>
        private string _htmlCode = string.Empty;
        /// <summary>The name of the currently playing video file.</summary>
        private string _videoName = string.Empty;
        /// <summary>The text detailing the current position in the currently playing video file.</summary>
        private string _position = string.Empty;
        /// <summary>The percentage of the playtime progressbar that is filled.</summary>
        private double _positionPercent = 0D;
        /// <summary>The visibility of the playtime progressbar.</summary>
        private bool _playtimeBarVisible = false;
        /// <summary>The <see cref="HttpClient"/> that is used to retrieve data from the web interface.</summary>
        private readonly HttpClient _httpClient;

        /// <summary>The base address, including port, to MPC-HC's web interface.</summary>
        private Uri _baseAddress = new Uri("http://127.0.0.1:13579/");
        /// <summary>Accessor for <see cref="_baseAddress"/>. The setter also sets the
        /// <see cref="HttpClient.BaseAddress"/> field for <see cref="_httpClient"/>.</summary>
        public Uri BaseAddress
        {
            get => _baseAddress;
            set
            {
                _httpClient.BaseAddress = value;
                _baseAddress = value;
            }
        }

        /// <summary>The timespan to wait for a response from the web interface.</summary>
        private TimeSpan _timeout = TimeSpan.FromSeconds(0.5);
        /// <summary>Accessor for <see cref="_timeout"/>. The setter also sets the
        /// <see cref="HttpClient.Timeout"/> field for <see cref="_httpClient"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The timeout specified is less than or equal to zero and is
        /// not <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>.</exception>
        /// <exception cref="InvalidOperationException">An operation has already been started on <see cref="_httpClient"/>.</exception>
        /// <exception cref="ObjectDisposedException"><see cref="_httpClient"/> has been disposed.</exception>
        public TimeSpan Timeout
        {
            get => _timeout;
            set
            {
                _httpClient.Timeout = value;
                _timeout = value;
            }
        }

        /// <summary></summary>
        private bool _runLoop = true;

        /// <summary>
        /// Constructor for the Updater class.
        /// </summary>
        /// <param name="timeTextBlock">The text block that displays the time.</param>
        /// <param name="videoNameTextBlock">The text block that displays the name of the video file being played.</param>
        /// <param name="playtimeTextBlock">The text block that displays the current and total playtime.</param>
        /// <param name="playtimeBar">The progressbar that displays the current playtime.</param>
        /// <param name="baseAddress">The base address, including port, to MPC-HC's web interface.</param>
        /// <param name="timeout">The timespan to wait for a response from the web interface.</param>
        public Updater(
            System.Windows.Controls.TextBlock timeTextBlock,
            System.Windows.Controls.TextBlock videoNameTextBlock,
            System.Windows.Controls.TextBlock playtimeTextBlock,
            System.Windows.Controls.ProgressBar playtimeBar,
            Uri baseAddress,
            TimeSpan timeout)
        {
            _timeTextBlock = timeTextBlock;
            _videoNameTextBlock = videoNameTextBlock;
            _playtimeTextBlock = playtimeTextBlock;
            _playtimeBar = playtimeBar;
            _httpClient = new HttpClient();
            BaseAddress = baseAddress;
            Timeout = timeout;
        }

        /// <summary>
        /// Starts the update loop that periodically reads the now playing variables from MPC-HC's web interface
        /// and updates the UI with the new values, as well as updating the clock to current time.
        /// </summary>
        public async void StartUpdateLoop()
        {
            UpdateTextBlockText(_videoNameTextBlock, _videoName);
            UpdateTextBlockText(_playtimeTextBlock, _position);
            UpdateProgressBar(_playtimeBar, _positionPercent);
            UpdateTextBlockText(_timeTextBlock, DateTime.Now.ToString("HH:mm"));

            Stopwatch stopwatch = Stopwatch.StartNew();
            
            while (_runLoop)
            {
                stopwatch.Restart();
                UpdateTextBlockText(_timeTextBlock, DateTime.Now.ToString("HH:mm"));
                
                await GetMpchcVariablesAsync();
                
                UpdateTextBlockText(_videoNameTextBlock, _videoName);
                UpdateTextBlockText(_playtimeTextBlock, _position);
                UpdateProgressBar(_playtimeBar, _positionPercent);
                SetElementVisibility(_playtimeBar, _playtimeBarVisible);

                int timeToSleep = 1000 - stopwatch.Elapsed.Milliseconds;
                Thread.Sleep(timeToSleep > 0 ? timeToSleep : 0);
            }
        }

        /// <summary>
        /// Stops the update loop from running by setting it's while condition to false.
        /// </summary>
        public void StopUpdateLoop()
        {
            _runLoop = false;
        }

        /// <summary>
        /// Calculates how large a part <paramref name="position"/> is of <paramref name="duration"/>, in percentage.
        /// </summary>
        /// <param name="position">The string representation of a fraction of <paramref name="duration"/>.</param>
        /// <param name="duration">The string representation of the total amount.</param>
        /// <returns>A percentage value of how large a part <paramref name="position"/> is of
        /// <paramref name="duration"/> -or- 0 if either <paramref name="position"/> or <paramref name="duration"/>
        /// cannot be parsed into an <see cref="Int32">int</see>, or <paramref name="duration"/> is 0.</returns>
        private static double CalculatePositionPercent(string position, string duration)
        {
            int pos;
            int dur;
            try
            {
                pos = int.Parse(position);
                dur = int.Parse(duration);
            }
            catch (Exception e) when(e is FormatException or OverflowException)
            {
                return 0D;
            }

            if (dur != 0)
            {
                return ((double) pos / dur) * 100;
            }

            return 0D;
        }

        /// <summary>
        /// Updates the text of a <see cref="System.Windows.Controls.TextBlock"/>.
        /// </summary>
        /// <param name="textBlock">The <see cref="System.Windows.Controls.TextBlock"/> to update.</param>
        /// <param name="newText">The new text.</param>
        private static void UpdateTextBlockText(System.Windows.Controls.TextBlock textBlock, string newText)
        {
            textBlock.Dispatcher.BeginInvoke(
                new Action(() => textBlock.Text = newText)
            );
        }

        /// <summary>
        /// Updates the value of a <see cref="System.Windows.Controls.ProgressBar"/>.
        /// </summary>
        /// <param name="progressBar">The <see cref="System.Windows.Controls.ProgressBar"/> to update.</param>
        /// <param name="newValue">The new value.</param>
        private static void UpdateProgressBar(System.Windows.Controls.ProgressBar progressBar, double newValue)
        {
            progressBar.Dispatcher.BeginInvoke(
                new Action(() => progressBar.Value = newValue)
            );
        }
        
        /// <summary>
        /// Sets the visibility of <paramref name="element"/>.
        /// </summary>
        /// <param name="element">The element to set the visibility of.</param>
        /// <param name="visible">If true sets the visibility of <paramref name="element"/> to
        /// <see cref="Visibility.Visible"/>, if false sets it to <see cref="Visibility.Hidden"/>.</param>
        private static void SetElementVisibility(UIElement element, bool visible)
        {
            element.Dispatcher.BeginInvoke(
                new Action(() => element.Visibility = visible ? Visibility.Visible : Visibility.Hidden)
            );
        }
        
        /// <summary>
        /// Retrieves the now playing variables from MPC-HC's web interface and stores them in the object.
        /// If the web interface is unavailable it sets the variables to empty values and hides the progress bar.
        /// </summary>
        private async Task GetMpchcVariablesAsync()
        {
            try
            {
                _htmlCode = await _httpClient.GetStringAsync(@"variables.html");
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                _htmlCode = string.Empty;
            }

            if (string.IsNullOrEmpty(_htmlCode))
            {
                _videoName = string.Empty;
                _positionPercent = 0D;
                _position = string.Empty;
                _playtimeBarVisible = false;
            }
            else
            {
                Task<string> videoNameTask = Task.Run(() => _htmlCode.GetBetween("<p id=\"file\">", "</p>"));
                Task<string> positionStringTask = Task.Run(() => _htmlCode.GetBetween("<p id=\"positionstring\">", "</p>"));
                Task<string> durationStringTask = Task.Run(() => _htmlCode.GetBetween("<p id=\"durationstring\">", "</p>"));
                Task<string> positionTask = Task.Run(() => _htmlCode.GetBetween("<p id=\"position\">", "</p>"));
                Task<string> durationTask = Task.Run(() => _htmlCode.GetBetween("<p id=\"duration\">", "</p>"));
                
                await Task.WhenAll(videoNameTask, positionStringTask, durationStringTask, positionTask, durationTask);
                
                _videoName = videoNameTask.Result;
                _positionPercent = CalculatePositionPercent(positionTask.Result, durationTask.Result);
                _position = $"{(int)_positionPercent}% - {positionStringTask.Result} / {durationStringTask.Result}";
                _playtimeBarVisible = true;
            }
        }
    }
}