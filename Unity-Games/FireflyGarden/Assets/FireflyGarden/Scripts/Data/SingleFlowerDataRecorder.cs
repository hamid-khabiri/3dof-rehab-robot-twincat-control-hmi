using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using UnityEngine;

namespace FireflyGarden.Data
{
    public sealed class SingleFlowerDataRecorder : MonoBehaviour
    {
        private const int MaximumFilenameAttempts = 1000;
        public const string CsvHeader = "schema_version,session_id,block_id,condition_id,configuration_version,random_seed,generator_version,trial_index_zero_based,trial_number_one_based,trial_id,mode,position_id,direction,distance_band,outcome,target_exit_count,runtime_position_swap_occurred,timeout_enabled,timeout_duration_s,initial_home_dwell_s,target_dwell_s,success_feedback_s,return_home_dwell_s,guidance_enabled,target_scale_multiplier,trial_start_timestamp_s,target_activation_timestamp_s,first_target_entry_timestamp_s,target_dwell_completed_timestamp_s,timeout_timestamp_s,return_completed_timestamp_s,time_to_first_target_entry_s,target_dwell_accumulation_span_s,target_acquisition_elapsed_s,return_phase_duration_s,total_trial_duration_s,correct_stimulus_id,nonmatching_stimulus_id,correct_target_slot,first_selected_target_slot,first_choice_correct,confirmed_incorrect_selection_count,assisted_after_error,target_slot_a_position_id,target_slot_b_position_id,cue_onset_timestamp_s,cue_offset_timestamp_s,choice_onset_timestamp_s,cue_exposure_duration_s,memory_delay_duration_s,abort_phase,anticipatory_exit_x,anticipatory_exit_y,anticipatory_exit_timestamp_s,elapsed_cue_exposure_at_abort_s";

        [Header("Session")]
        [SerializeField] private string sessionIdOverride;
        [SerializeField] private bool generateSessionIdAutomatically = true;
        [Header("Runtime Debugging")]
        [SerializeField] private string sessionId;
        [SerializeField] private string outputFilePath;
        [SerializeField] private bool isInitialized;
        [SerializeField] private int rowsWritten;
        [SerializeField] private string lastError;

        private StreamWriter writer;
        private readonly HashSet<string> recordedTrialIds = new HashSet<string>(StringComparer.Ordinal);
        private bool closed;
        private bool errorLogged;

        public string SessionId => sessionId;
        public string OutputFilePath => outputFilePath;
        public bool IsInitialized => isInitialized;
        public bool IsAcceptingRows => isInitialized && !closed && writer != null;
        public int RowsWritten => rowsWritten;
        public string LastError => lastError;

        private void Awake() => Initialize();

        public bool TryRecord(TrialSummary summary, out string error)
        {
            error = string.Empty;
            if (!IsAcceptingRows) return FailWrite("The trial recorder is not accepting rows.", out error);
            if (summary == null) return FailWrite("A null trial summary cannot be recorded.", out error);
            if (!string.Equals(summary.SessionId, sessionId, StringComparison.Ordinal)) return FailWrite("The trial summary session ID does not match the active recorder session.", out error);
            if (string.IsNullOrWhiteSpace(summary.TrialId)) return FailWrite("The trial summary has no trial ID.", out error);
            if (recordedTrialIds.Contains(summary.TrialId)) return FailWrite("Duplicate trial ID was rejected: " + summary.TrialId, out error);

            try
            {
                writer.WriteLine(BuildCsvRow(summary));
                writer.Flush();
                recordedTrialIds.Add(summary.TrialId);
                rowsWritten++;
                return true;
            }
            catch (IOException exception) { return FailWrite("Could not write or flush the trial CSV.", exception, out error); }
            catch (ObjectDisposedException exception) { return FailWrite("The trial CSV was already closed.", exception, out error); }
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            isInitialized = false;
            if (writer == null) return;
            try { writer.Dispose(); }
            catch (IOException exception) { SetError("Could not close the trial CSV cleanly.", exception); }
            finally { writer = null; }
        }

        private void Initialize()
        {
            if (isInitialized || closed) return;
            try
            {
                sessionId = ResolveSessionId();
                string directory = Path.Combine(Application.persistentDataPath, "FireflyGarden", "Data");
                Directory.CreateDirectory(directory);
                outputFilePath = FindAvailablePath(directory, sessionId);
                writer = new StreamWriter(new FileStream(outputFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
                writer.WriteLine(CsvHeader);
                writer.Flush();
                isInitialized = true;
                lastError = string.Empty;
            }
            catch (IOException exception) { InitializationFailed("Could not create or initialize the trial CSV.", exception); }
            catch (UnauthorizedAccessException exception) { InitializationFailed("Access to the trial CSV directory was denied.", exception); }
            catch (SecurityException exception) { InitializationFailed("Security settings prevented trial CSV creation.", exception); }
            catch (ArgumentException exception) { InitializationFailed("The trial CSV path is invalid.", exception); }
            catch (NotSupportedException exception) { InitializationFailed("The trial CSV path format is unsupported.", exception); }
        }

        private string ResolveSessionId()
        {
            if (generateSessionIdAutomatically || string.IsNullOrWhiteSpace(sessionIdOverride))
                return "FG_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);

            string value = sessionIdOverride.Trim();
            var sanitized = new StringBuilder(value.Length);
            foreach (char character in value)
                if (char.IsLetterOrDigit(character) || character == '-' || character == '_') sanitized.Append(character);
                else sanitized.Append('_');
            string result = sanitized.ToString().Trim('_');
            if (result.Length == 0) throw new ArgumentException("The session ID override has no usable filename characters.");
            return result;
        }

        private static string FindAvailablePath(string directory, string id)
        {
            for (int attempt = 0; attempt < MaximumFilenameAttempts; attempt++)
            {
                string suffix = attempt == 0 ? string.Empty : "_" + attempt.ToString("D3", CultureInfo.InvariantCulture);
                string candidate = Path.Combine(directory, id + "_trials" + suffix + ".csv");
                if (!File.Exists(candidate)) return candidate;
            }
            throw new IOException("No unused trial CSV filename was available after a bounded search.");
        }

        private static string BuildCsvRow(TrialSummary value)
        {
            string[] fields =
            {
                value.SchemaVersion, value.SessionId, value.BlockId, value.ConditionId,
                Integer(value.ConfigurationVersion), Integer(value.RandomSeed), value.GeneratorVersion,
                Integer(value.TrialIndexZeroBased), Integer(value.TrialNumberOneBased), value.TrialId,
                value.Mode, value.PositionId, value.Direction.ToString(), value.DistanceBand.ToString(), value.Outcome.ToString(),
                Integer(value.TargetExitCount), Boolean(value.RuntimePositionSwapOccurred), Boolean(value.TimeoutEnabled),
                Float(value.TimeoutDuration), Float(value.InitialHomeDwellDuration), Float(value.TargetDwellDuration),
                Float(value.SuccessFeedbackDuration), Float(value.ReturnHomeDwellDuration), Boolean(value.GuidanceEnabled),
                Float(value.TargetScaleMultiplier), Double(value.TrialStartTimestamp), Double(value.TargetActivationTimestamp),
                Double(value.FirstTargetEntryTimestamp), Double(value.TargetDwellCompletedTimestamp), Double(value.TimeoutTimestamp),
                Double(value.ReturnCompletedTimestamp), Double(value.TimeToFirstTargetEntry), Double(value.TargetDwellAccumulationSpan),
                Double(value.TargetAcquisitionElapsed), Double(value.ReturnPhaseDuration), Double(value.TotalTrialDuration),
                value.CorrectStimulusId, value.NonmatchingStimulusId, value.CorrectTargetSlot,
                value.FirstSelectedTargetSlot, value.FirstChoiceCorrect,
                Integer(value.ConfirmedIncorrectSelectionCount), Boolean(value.AssistedAfterError),
                value.TargetAPositionId, value.TargetBPositionId,
                Double(value.CueOnsetTimestamp), Double(value.CueOffsetTimestamp), Double(value.ChoiceOnsetTimestamp),
                Float(value.CueExposureDuration), Float(value.MemoryDelayDuration), value.AbortPhase,
                Float(value.AnticipatoryExitX), Float(value.AnticipatoryExitY),
                Double(value.AnticipatoryExitTimestamp), Float(value.ElapsedCueExposureAtAbort)
            };
            for (int i = 0; i < fields.Length; i++) fields[i] = Escape(fields[i]);
            return string.Join(",", fields);
        }

        private static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Float(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Boolean(bool value) => value ? "true" : "false";
        private static string Double(double value) => value == TrialSummary.UnavailableValue
            ? string.Empty : value.ToString("R", CultureInfo.InvariantCulture);

        private static string Escape(string value)
        {
            value = value ?? string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private bool FailWrite(string message, out string error) { SetError(message, null); CloseWriterAfterFailure(); error = lastError; return false; }
        private bool FailWrite(string message, Exception exception, out string error) { SetError(message, exception); CloseWriterAfterFailure(); error = lastError; return false; }
        private void InitializationFailed(string message, Exception exception) { closed = true; isInitialized = false; SetError(message, exception); CloseWriterAfterFailure(); }
        private void CloseWriterAfterFailure() { if (writer == null) return; try { writer.Dispose(); } catch (IOException) { } writer = null; }

        private void SetError(string message, Exception exception)
        {
            lastError = exception == null ? message : message + " " + exception.Message;
            isInitialized = false;
            closed = true;
            if (!errorLogged) { errorLogged = true; Debug.LogError("Firefly Garden data recording error: " + lastError, this); }
        }

        private void OnDisable() => Close();
        private void OnDestroy() => Close();
        private void OnApplicationQuit() => Close();
    }
}
