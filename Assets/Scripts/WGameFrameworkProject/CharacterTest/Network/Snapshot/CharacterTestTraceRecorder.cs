using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.CharacterControl;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestTraceRecorder : IDisposable
    {
        private readonly List<CharacterTestTraceEvent> _recentEvents;
        private readonly char[] _fileWriteNumberBuffer = new char[32];
        private readonly int _capacity;
        private readonly string _filePath;
        private StreamWriter _fileWriter;
        private bool _fileWriteFailed;

        public CharacterTestTraceRecorder(
            int capacity = 80,
            string filePath = "",
            bool fallbackToUniqueFilePath = false)
        {
            _capacity = Math.Max(8, capacity);
            _recentEvents = new List<CharacterTestTraceEvent>(_capacity);
            _filePath = PrepareFilePath(filePath ?? string.Empty, fallbackToUniqueFilePath);
        }

        public IReadOnlyList<CharacterTestTraceEvent> RecentEvents => _recentEvents;
        public string FilePath => _filePath;
        public bool WritesFile => !string.IsNullOrEmpty(_filePath) && !_fileWriteFailed;

        public static CharacterTestTraceRecorder CreateDefault()
        {
            return new CharacterTestTraceRecorder(
                capacity: 80,
                filePath: CreateDefaultFilePath(Application.persistentDataPath),
                fallbackToUniqueFilePath: true);
        }

        public static CharacterTestTraceRecorder CreateInMemory(int capacity = 80)
        {
            return new CharacterTestTraceRecorder(capacity);
        }

        internal static string CreateDefaultFilePath(string persistentDataPath, string[] commandLineArgs = null)
        {
            string directory = Path.Combine(persistentDataPath ?? string.Empty, "CharacterTest");
            return Path.Combine(directory, ResolveDefaultFileName(commandLineArgs));
        }

        internal static string CreateProcessUniqueFilePath(string requestedPath, int processId)
        {
            string directory = Path.GetDirectoryName(requestedPath);
            string name = Path.GetFileNameWithoutExtension(requestedPath);
            string extension = Path.GetExtension(requestedPath);
            string fileName = name
                + "-pid"
                + Math.Max(0, processId).ToString(CultureInfo.InvariantCulture)
                + extension;
            return string.IsNullOrEmpty(directory)
                ? fileName
                : Path.Combine(directory, fileName);
        }

        /// <remarks>AllocByDesign for low-frequency diagnostics; use typed trace overloads on runtime hot paths.</remarks>
        public void JumpInput(RuntimeFrame frame, int entityId, string message)
        {
            Record(frame, "Jump", "input", entityId, message, warning: false);
        }

        /// <remarks>AllocByDesign for low-frequency diagnostics; use typed trace overloads on runtime hot paths.</remarks>
        public void JumpAction(RuntimeFrame frame, int entityId, string message, bool warning = false)
        {
            Record(frame, "Jump", "action", entityId, message, warning);
        }

        /// <remarks>AllocByDesign for low-frequency diagnostics; use typed trace overloads on runtime hot paths.</remarks>
        public void JumpMotion(RuntimeFrame frame, int entityId, string message, bool warning = false)
        {
            Record(frame, "Jump", "motion", entityId, message, warning);
        }

        /// <remarks>AllocByDesign for low-frequency diagnostics; use typed trace overloads on runtime hot paths.</remarks>
        public void AttackInput(RuntimeFrame frame, int entityId, string message)
        {
            Record(frame, "Attack", "input", entityId, message, warning: false);
        }

        /// <remarks>AllocByDesign for low-frequency diagnostics; use typed trace overloads on runtime hot paths.</remarks>
        public void AttackAction(RuntimeFrame frame, int entityId, string message, bool warning = false)
        {
            Record(frame, "Attack", "action", entityId, message, warning);
        }

        /// <remarks>AllocByDesign for low-frequency diagnostics; use typed trace overloads on runtime hot paths.</remarks>
        public void AttackAnimation(RuntimeFrame frame, int entityId, string message, bool warning = false)
        {
            Record(frame, "Attack", "animation", entityId, message, warning);
        }

        /// <remarks>AllocByDesign for low-frequency diagnostics; use typed trace overloads on runtime hot paths.</remarks>
        public void AttackView(RuntimeFrame frame, int entityId, string message, bool warning = false)
        {
            Record(frame, "Attack", "view", entityId, message, warning);
        }

        /// <remarks>AllocByDesign for low-frequency diagnostics; use typed trace overloads on runtime hot paths.</remarks>
        public void AttackCombat(RuntimeFrame frame, int entityId, string message, bool warning = false)
        {
            Record(frame, "Attack", "combat", entityId, message, warning);
        }

        /// <remarks>AllocByDesign for low-frequency diagnostics; use typed trace overloads on runtime hot paths.</remarks>
        public void Locomotion(RuntimeFrame frame, int entityId, string message, bool warning = false)
        {
            Record(frame, "Locomotion", "state", entityId, message, warning);
        }

        internal void AttackCombatHit(
            RuntimeFrame frame,
            int entityId,
            in CharacterTestCombatHitTraceEvent trace)
        {
            Record(CharacterTestTraceEvent.CreateCombatHit(frame.Value, entityId, trace));
        }

        internal void AttackCombatHitQuery(
            RuntimeFrame frame,
            in CharacterTestCombatHitQueryTraceEvent trace)
        {
            Record(CharacterTestTraceEvent.CreateCombatHitQuery(frame.Value, trace));
        }

        internal void AttackCombatScatterPressure(
            RuntimeFrame frame,
            int entityId,
            in CharacterTestScatterPressureTraceEvent trace)
        {
            Record(CharacterTestTraceEvent.CreateScatterPressure(frame.Value, entityId, trace));
        }

        internal void AttackCombatCohesionBreak(
            RuntimeFrame frame,
            int entityId,
            in CharacterTestCohesionBreakTraceEvent trace)
        {
            Record(CharacterTestTraceEvent.CreateCohesionBreak(frame.Value, entityId, trace));
        }

        internal void LocomotionControlScale(
            RuntimeFrame frame,
            int entityId,
            in CharacterTestMotionControlScaleTraceEvent trace)
        {
            Record(CharacterTestTraceEvent.CreateMotionControlScale(frame.Value, entityId, trace));
        }

        internal void JumpMotionImpulse(
            RuntimeFrame frame,
            int entityId,
            in CharacterTestJumpMotionTraceEvent trace)
        {
            Record(CharacterTestTraceEvent.CreateJumpMotion(frame.Value, entityId, trace));
        }

        internal void LocomotionRootMotionDelta(
            RuntimeFrame frame,
            int entityId,
            in CharacterTestRootMotionDeltaTraceEvent trace)
        {
            Record(CharacterTestTraceEvent.CreateRootMotionDelta(frame.Value, entityId, trace));
        }

        internal void LocomotionRootMotionActive(
            RuntimeFrame frame,
            int entityId,
            in CharacterTestRootMotionActiveTraceEvent trace)
        {
            Record(CharacterTestTraceEvent.CreateRootMotionActive(frame.Value, entityId, trace));
        }

        internal void LocomotionCollisionProfileBlocked(
            RuntimeFrame frame,
            int entityId,
            in CharacterTestCollisionProfileBlockedTraceEvent trace)
        {
            Record(CharacterTestTraceEvent.CreateCollisionProfileBlocked(frame.Value, entityId, trace));
        }

        internal void AttackInput(
            RuntimeFrame frame,
            int entityId,
            in CharacterTestActionInputTraceEvent trace)
        {
            Record(CharacterTestTraceEvent.CreateAttackInput(frame.Value, entityId, trace));
        }

        internal void JumpInput(
            RuntimeFrame frame,
            int entityId,
            in CharacterTestActionInputTraceEvent trace)
        {
            Record(CharacterTestTraceEvent.CreateJumpInput(frame.Value, entityId, trace));
        }

        internal void AttackAction(
            RuntimeFrame frame,
            int entityId,
            in CharacterTestActionOutcomeTraceEvent trace)
        {
            Record(CharacterTestTraceEvent.CreateAttackAction(frame.Value, entityId, trace));
        }

        internal void JumpAction(
            RuntimeFrame frame,
            int entityId,
            in CharacterTestActionOutcomeTraceEvent trace)
        {
            Record(CharacterTestTraceEvent.CreateJumpAction(frame.Value, entityId, trace));
        }

        /// <remarks>AllocByDesign for low-frequency diagnostics; use typed trace overloads on runtime hot paths.</remarks>
        internal void Lifecycle(RuntimeFrame frame, int entityId, string message, bool warning = false)
        {
            Record(frame, "Lifecycle", "state", entityId, message, warning);
        }

        /// <remarks>NoAlloc; closes the owned trace file writer when one has been opened.</remarks>
        public void Dispose()
        {
            DisposeFileWriter();
        }

        private void Record(RuntimeFrame frame, string category, string stage, int entityId, string message, bool warning)
        {
            Record(new CharacterTestTraceEvent(
                frame.Value,
                category ?? string.Empty,
                stage ?? string.Empty,
                entityId,
                message ?? string.Empty,
                warning));
        }

        private void Record(in CharacterTestTraceEvent evt)
        {
            if (_recentEvents.Count >= _capacity)
                _recentEvents.RemoveAt(0);
            _recentEvents.Add(evt);

            if (!string.IsNullOrEmpty(_filePath) && !_fileWriteFailed)
                AppendEventLine(evt);
        }

        private void AppendEventLine(in CharacterTestTraceEvent evt)
        {
            try
            {
                StreamWriter writer = GetFileWriter();
                if (writer == null)
                    return;

                evt.WriteJsonLine(writer, _fileWriteNumberBuffer);
                writer.WriteLine();
                writer.Flush();
            }
            catch (IOException)
            {
                MarkFileWriteFailed();
            }
            catch (UnauthorizedAccessException)
            {
                MarkFileWriteFailed();
            }
        }

        private StreamWriter GetFileWriter()
        {
            if (_fileWriter != null)
                return _fileWriter;

            if (string.IsNullOrEmpty(_filePath) || _fileWriteFailed)
                return null;

            try
            {
                _fileWriter = new StreamWriter(
                    new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read));
                return _fileWriter;
            }
            catch (IOException)
            {
                _fileWriteFailed = true;
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                _fileWriteFailed = true;
                return null;
            }
        }

        private void MarkFileWriteFailed()
        {
            _fileWriteFailed = true;
            DisposeFileWriter();
        }

        private void DisposeFileWriter()
        {
            StreamWriter writer = _fileWriter;
            if (writer == null)
                return;

            _fileWriter = null;
            try
            {
                writer.Dispose();
            }
            catch (IOException)
            {
                _fileWriteFailed = true;
            }
            catch (UnauthorizedAccessException)
            {
                _fileWriteFailed = true;
            }
        }

        private static string PrepareFilePath(string filePath, bool fallbackToUniqueFilePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return string.Empty;

            try
            {
                ResetTraceFile(filePath);
                return filePath;
            }
            catch (IOException) when (fallbackToUniqueFilePath)
            {
                return PrepareFallbackFilePath(filePath);
            }
            catch (UnauthorizedAccessException) when (fallbackToUniqueFilePath)
            {
                return PrepareFallbackFilePath(filePath);
            }
        }

        private static string PrepareFallbackFilePath(string requestedPath)
        {
            string fallbackPath = CreateProcessUniqueFilePath(requestedPath, ResolveCurrentProcessId());
            try
            {
                ResetTraceFile(fallbackPath);
                return fallbackPath;
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        private static void ResetTraceFile(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
            }
        }

        private static int ResolveCurrentProcessId()
        {
            try
            {
                return System.Diagnostics.Process.GetCurrentProcess().Id;
            }
            catch
            {
                return 0;
            }
        }

        private static string ResolveDefaultFileName(string[] commandLineArgs)
        {
            string playerName = ResolveMultiplayerPlayModePlayerName(commandLineArgs);
            if (string.IsNullOrEmpty(playerName)
                || string.Equals(playerName, "Player1", StringComparison.OrdinalIgnoreCase))
            {
                return "jump-trace.jsonl";
            }

            return "jump-trace-" + SanitizeFileNameSegment(playerName) + ".jsonl";
        }

        private static string ResolveMultiplayerPlayModePlayerName(string[] commandLineArgs)
        {
            string[] args = commandLineArgs ?? Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], "-name", StringComparison.OrdinalIgnoreCase))
                    continue;

                string value = args[i + 1];
                if (value != null && value.StartsWith("Player", StringComparison.OrdinalIgnoreCase))
                    return value;
            }

            return string.Empty;
        }

        private static string SanitizeFileNameSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "player";

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                    chars[i] = '_';
            }

            string sanitized = new string(chars).Trim('.');
            return string.IsNullOrEmpty(sanitized) ? "player" : sanitized;
        }
    }

    public readonly struct CharacterTestTraceEvent
    {
        private readonly string _category;
        private readonly string _stage;
        private readonly string _message;
        private readonly CharacterTestTraceEventKind _kind;
        private readonly CharacterTestCombatHitTraceEvent _combatHitTrace;
        private readonly CharacterTestCombatHitQueryTraceEvent _combatHitQueryTrace;
        private readonly CharacterTestScatterPressureTraceEvent _scatterPressureTrace;
        private readonly CharacterTestCohesionBreakTraceEvent _cohesionBreakTrace;
        private readonly CharacterTestMotionControlScaleTraceEvent _motionControlScaleTrace;
        private readonly CharacterTestJumpMotionTraceEvent _jumpMotionTrace;
        private readonly CharacterTestRootMotionDeltaTraceEvent _rootMotionDeltaTrace;
        private readonly CharacterTestRootMotionActiveTraceEvent _rootMotionActiveTrace;
        private readonly CharacterTestCollisionProfileBlockedTraceEvent _collisionProfileBlockedTrace;
        private readonly CharacterTestActionInputTraceEvent _actionInputTrace;
        private readonly CharacterTestActionOutcomeTraceEvent _actionOutcomeTrace;

        public CharacterTestTraceEvent(
            long frame,
            string category,
            string stage,
            int entityId,
            string message,
            bool warning)
        {
            Frame = frame;
            _category = category ?? string.Empty;
            _stage = stage ?? string.Empty;
            EntityId = entityId;
            _message = message ?? string.Empty;
            Warning = warning;
            _kind = CharacterTestTraceEventKind.Text;
            _combatHitTrace = default;
            _combatHitQueryTrace = default;
            _scatterPressureTrace = default;
            _cohesionBreakTrace = default;
            _motionControlScaleTrace = default;
            _jumpMotionTrace = default;
            _rootMotionDeltaTrace = default;
            _rootMotionActiveTrace = default;
            _collisionProfileBlockedTrace = default;
            _actionInputTrace = default;
            _actionOutcomeTrace = default;
        }

        private CharacterTestTraceEvent(
            long frame,
            string category,
            string stage,
            int entityId,
            bool warning,
            CharacterTestTraceEventKind kind,
            CharacterTestCombatHitTraceEvent combatHitTrace = default,
            CharacterTestCombatHitQueryTraceEvent combatHitQueryTrace = default,
            CharacterTestScatterPressureTraceEvent scatterPressureTrace = default,
            CharacterTestCohesionBreakTraceEvent cohesionBreakTrace = default,
            CharacterTestMotionControlScaleTraceEvent motionControlScaleTrace = default,
            CharacterTestJumpMotionTraceEvent jumpMotionTrace = default,
            CharacterTestRootMotionDeltaTraceEvent rootMotionDeltaTrace = default,
            CharacterTestRootMotionActiveTraceEvent rootMotionActiveTrace = default,
            CharacterTestCollisionProfileBlockedTraceEvent collisionProfileBlockedTrace = default,
            CharacterTestActionInputTraceEvent actionInputTrace = default,
            CharacterTestActionOutcomeTraceEvent actionOutcomeTrace = default)
        {
            Frame = frame;
            _category = category ?? string.Empty;
            _stage = stage ?? string.Empty;
            EntityId = entityId;
            _message = string.Empty;
            Warning = warning;
            _kind = kind;
            _combatHitTrace = combatHitTrace;
            _combatHitQueryTrace = combatHitQueryTrace;
            _scatterPressureTrace = scatterPressureTrace;
            _cohesionBreakTrace = cohesionBreakTrace;
            _motionControlScaleTrace = motionControlScaleTrace;
            _jumpMotionTrace = jumpMotionTrace;
            _rootMotionDeltaTrace = rootMotionDeltaTrace;
            _rootMotionActiveTrace = rootMotionActiveTrace;
            _collisionProfileBlockedTrace = collisionProfileBlockedTrace;
            _actionInputTrace = actionInputTrace;
            _actionOutcomeTrace = actionOutcomeTrace;
        }

        public long Frame { get; }
        public string Category => _category ?? string.Empty;
        public string Stage => _stage ?? string.Empty;
        public int EntityId { get; }
        internal CharacterTestTraceEventKind Kind => _kind;
        internal CharacterTestCombatHitTraceEvent CombatHitTrace => _combatHitTrace;
        internal CharacterTestActionInputTraceEvent ActionInputTrace => _actionInputTrace;
        internal CharacterTestActionOutcomeTraceEvent ActionOutcomeTrace => _actionOutcomeTrace;
        /// <remarks>AllocByDesign for structured trace events; steady-state recording and file writes do not read this property.</remarks>
        public string Message => _kind == CharacterTestTraceEventKind.Text ? _message ?? string.Empty : BuildMessage();
        public bool Warning { get; }

        internal static CharacterTestTraceEvent CreateCombatHit(
            long frame,
            int entityId,
            in CharacterTestCombatHitTraceEvent trace)
        {
            return new CharacterTestTraceEvent(
                frame,
                "Attack",
                "combat",
                entityId,
                warning: false,
                CharacterTestTraceEventKind.CombatHit,
                combatHitTrace: trace);
        }

        internal static CharacterTestTraceEvent CreateCombatHitQuery(
            long frame,
            in CharacterTestCombatHitQueryTraceEvent trace)
        {
            return new CharacterTestTraceEvent(
                frame,
                "Attack",
                "combat",
                0,
                warning: false,
                CharacterTestTraceEventKind.CombatHitQuery,
                combatHitQueryTrace: trace);
        }

        internal static CharacterTestTraceEvent CreateScatterPressure(
            long frame,
            int entityId,
            in CharacterTestScatterPressureTraceEvent trace)
        {
            return new CharacterTestTraceEvent(
                frame,
                "Attack",
                "combat",
                entityId,
                warning: false,
                CharacterTestTraceEventKind.ScatterPressure,
                scatterPressureTrace: trace);
        }

        internal static CharacterTestTraceEvent CreateCohesionBreak(
            long frame,
            int entityId,
            in CharacterTestCohesionBreakTraceEvent trace)
        {
            return new CharacterTestTraceEvent(
                frame,
                "Attack",
                "combat",
                entityId,
                warning: false,
                CharacterTestTraceEventKind.CohesionBreak,
                cohesionBreakTrace: trace);
        }

        internal static CharacterTestTraceEvent CreateMotionControlScale(
            long frame,
            int entityId,
            in CharacterTestMotionControlScaleTraceEvent trace)
        {
            return new CharacterTestTraceEvent(
                frame,
                "Locomotion",
                "state",
                entityId,
                warning: false,
                CharacterTestTraceEventKind.MotionControlScale,
                motionControlScaleTrace: trace);
        }

        internal static CharacterTestTraceEvent CreateJumpMotion(
            long frame,
            int entityId,
            in CharacterTestJumpMotionTraceEvent trace)
        {
            return new CharacterTestTraceEvent(
                frame,
                "Jump",
                "motion",
                entityId,
                trace.Blocked,
                CharacterTestTraceEventKind.JumpMotion,
                jumpMotionTrace: trace);
        }

        internal static CharacterTestTraceEvent CreateRootMotionDelta(
            long frame,
            int entityId,
            in CharacterTestRootMotionDeltaTraceEvent trace)
        {
            return new CharacterTestTraceEvent(
                frame,
                "Locomotion",
                "state",
                entityId,
                warning: false,
                CharacterTestTraceEventKind.RootMotionDelta,
                rootMotionDeltaTrace: trace);
        }

        internal static CharacterTestTraceEvent CreateRootMotionActive(
            long frame,
            int entityId,
            in CharacterTestRootMotionActiveTraceEvent trace)
        {
            return new CharacterTestTraceEvent(
                frame,
                "Locomotion",
                "state",
                entityId,
                warning: false,
                CharacterTestTraceEventKind.RootMotionActive,
                rootMotionActiveTrace: trace);
        }

        internal static CharacterTestTraceEvent CreateCollisionProfileBlocked(
            long frame,
            int entityId,
            in CharacterTestCollisionProfileBlockedTraceEvent trace)
        {
            return new CharacterTestTraceEvent(
                frame,
                "Locomotion",
                "state",
                entityId,
                warning: true,
                CharacterTestTraceEventKind.CollisionProfileBlocked,
                collisionProfileBlockedTrace: trace);
        }

        internal static CharacterTestTraceEvent CreateAttackInput(
            long frame,
            int entityId,
            in CharacterTestActionInputTraceEvent trace)
        {
            return new CharacterTestTraceEvent(
                frame,
                "Attack",
                "input",
                entityId,
                warning: false,
                CharacterTestTraceEventKind.ActionInput,
                actionInputTrace: trace);
        }

        internal static CharacterTestTraceEvent CreateJumpInput(
            long frame,
            int entityId,
            in CharacterTestActionInputTraceEvent trace)
        {
            return new CharacterTestTraceEvent(
                frame,
                "Jump",
                "input",
                entityId,
                warning: false,
                CharacterTestTraceEventKind.ActionInput,
                actionInputTrace: trace);
        }

        internal static CharacterTestTraceEvent CreateAttackAction(
            long frame,
            int entityId,
            in CharacterTestActionOutcomeTraceEvent trace)
        {
            return new CharacterTestTraceEvent(
                frame,
                "Attack",
                "action",
                entityId,
                trace.Warning,
                CharacterTestTraceEventKind.ActionOutcome,
                actionOutcomeTrace: trace);
        }

        internal static CharacterTestTraceEvent CreateJumpAction(
            long frame,
            int entityId,
            in CharacterTestActionOutcomeTraceEvent trace)
        {
            return new CharacterTestTraceEvent(
                frame,
                "Jump",
                "action",
                entityId,
                trace.Warning,
                CharacterTestTraceEventKind.ActionOutcome,
                actionOutcomeTrace: trace);
        }

        public string ToDisplayString()
        {
            return "f" + Frame.ToString(CultureInfo.InvariantCulture)
                + " e" + EntityId.ToString(CultureInfo.InvariantCulture)
                + " " + Category
                + "." + Stage
                + " " + Message;
        }

        /// <remarks>AllocByDesign diagnostic helper; recorder-owned file output uses a writer-based path.</remarks>
        public string ToJsonLine()
        {
            return "{\"frame\":" + Frame.ToString(CultureInfo.InvariantCulture)
                + ",\"category\":\"" + Escape(Category)
                + "\",\"stage\":\"" + Escape(Stage)
                + "\",\"entity\":" + EntityId.ToString(CultureInfo.InvariantCulture)
                + ",\"warning\":" + (Warning ? "true" : "false")
                + ",\"message\":\"" + Escape(Message)
                + "\"}";
        }

        internal void WriteJsonLine(TextWriter writer, char[] numberBuffer)
        {
            writer.Write("{\"frame\":");
            WriteInt64(writer, Frame, numberBuffer);
            writer.Write(",\"category\":\"");
            WriteEscapedString(writer, Category);
            writer.Write("\",\"stage\":\"");
            WriteEscapedString(writer, Stage);
            writer.Write("\",\"entity\":");
            WriteInt64(writer, EntityId, numberBuffer);
            writer.Write(",\"warning\":");
            writer.Write(Warning ? "true" : "false");
            writer.Write(",\"message\":\"");
            WriteEscapedMessage(writer, numberBuffer);
            writer.Write("\"}");
        }

        private string BuildMessage()
        {
            var builder = new StringBuilder(256);
            AppendMessage(builder);
            return builder.ToString();
        }

        private void AppendMessage(StringBuilder builder)
        {
            switch (_kind)
            {
                case CharacterTestTraceEventKind.CombatHit:
                    AppendCombatHitMessage(builder, _combatHitTrace);
                    break;
                case CharacterTestTraceEventKind.CombatHitQuery:
                    AppendCombatHitQueryMessage(builder, _combatHitQueryTrace);
                    break;
                case CharacterTestTraceEventKind.ScatterPressure:
                    AppendScatterPressureMessage(builder, _scatterPressureTrace);
                    break;
                case CharacterTestTraceEventKind.CohesionBreak:
                    AppendCohesionBreakMessage(builder, _cohesionBreakTrace);
                    break;
                case CharacterTestTraceEventKind.MotionControlScale:
                    AppendMotionControlScaleMessage(builder, _motionControlScaleTrace);
                    break;
                case CharacterTestTraceEventKind.JumpMotion:
                    AppendJumpMotionMessage(builder, _jumpMotionTrace);
                    break;
                case CharacterTestTraceEventKind.RootMotionDelta:
                    AppendRootMotionDeltaMessage(builder, _rootMotionDeltaTrace);
                    break;
                case CharacterTestTraceEventKind.RootMotionActive:
                    AppendRootMotionActiveMessage(builder, _rootMotionActiveTrace);
                    break;
                case CharacterTestTraceEventKind.CollisionProfileBlocked:
                    AppendCollisionProfileBlockedMessage(builder, _collisionProfileBlockedTrace);
                    break;
                case CharacterTestTraceEventKind.ActionInput:
                    AppendActionInputMessage(builder, _actionInputTrace);
                    break;
                case CharacterTestTraceEventKind.ActionOutcome:
                    AppendActionOutcomeMessage(builder, _actionOutcomeTrace);
                    break;
                default:
                    builder.Append(_message ?? string.Empty);
                    break;
            }
        }

        private void WriteEscapedMessage(TextWriter writer, char[] numberBuffer)
        {
            switch (_kind)
            {
                case CharacterTestTraceEventKind.CombatHit:
                    WriteCombatHitMessage(writer, _combatHitTrace, numberBuffer);
                    break;
                case CharacterTestTraceEventKind.CombatHitQuery:
                    WriteCombatHitQueryMessage(writer, _combatHitQueryTrace, numberBuffer);
                    break;
                case CharacterTestTraceEventKind.ScatterPressure:
                    WriteScatterPressureMessage(writer, _scatterPressureTrace, numberBuffer);
                    break;
                case CharacterTestTraceEventKind.CohesionBreak:
                    WriteCohesionBreakMessage(writer, _cohesionBreakTrace, numberBuffer);
                    break;
                case CharacterTestTraceEventKind.MotionControlScale:
                    WriteMotionControlScaleMessage(writer, _motionControlScaleTrace, numberBuffer);
                    break;
                case CharacterTestTraceEventKind.JumpMotion:
                    WriteJumpMotionMessage(writer, _jumpMotionTrace, numberBuffer);
                    break;
                case CharacterTestTraceEventKind.RootMotionDelta:
                    WriteRootMotionDeltaMessage(writer, _rootMotionDeltaTrace, numberBuffer);
                    break;
                case CharacterTestTraceEventKind.RootMotionActive:
                    WriteRootMotionActiveMessage(writer, _rootMotionActiveTrace, numberBuffer);
                    break;
                case CharacterTestTraceEventKind.CollisionProfileBlocked:
                    WriteCollisionProfileBlockedMessage(writer, _collisionProfileBlockedTrace, numberBuffer);
                    break;
                case CharacterTestTraceEventKind.ActionInput:
                    WriteActionInputMessage(writer, _actionInputTrace, numberBuffer);
                    break;
                case CharacterTestTraceEventKind.ActionOutcome:
                    WriteActionOutcomeMessage(writer, _actionOutcomeTrace, numberBuffer);
                    break;
                default:
                    WriteEscapedString(writer, _message ?? string.Empty);
                    break;
            }
        }

        private static void AppendCombatHitMessage(StringBuilder builder, CharacterTestCombatHitTraceEvent trace)
        {
            builder.Append("hitTrace hit target=");
            builder.Append(trace.TargetEntityId);
            builder.Append(" kind=");
            AppendHitResolveKind(builder, trace.Kind);
            builder.Append(" damageSource=");
            AppendTraceTextOrNone(builder, trace.DamageSourceId);
            builder.Append(" damage=");
            builder.Append(trace.Damage);
            builder.Append(" hp=");
            if (trace.HasTarget)
            {
                builder.Append(trace.Health);
                builder.Append('/');
                builder.Append(trace.MaxHealth);
            }
            else
            {
                builder.Append("n/a");
            }

            builder.Append(" scatterPressure=");
            if (trace.HasTarget)
            {
                builder.Append(trace.ScatterPressure);
                builder.Append('/');
                builder.Append(trace.MaxScatterPressure);
                builder.Append('(');
                AppendPressureBand(builder, trace.PressureBand);
                builder.Append(')');
            }
            else
            {
                builder.Append("n/a");
            }

            builder.Append(" stagger=");
            builder.Append(trace.HasTarget ? trace.StaggerFrames.ToString(CultureInfo.InvariantCulture) : "n/a");
            builder.Append(" hitPoint=");
            AppendFixVector3(builder, trace.HitPointWorld);
            builder.Append(" hitNormal=");
            AppendFixVector3(builder, trace.HitNormalWorld);
            builder.Append(" trace=");
            builder.Append(trace.TraceId);
        }

        private static void WriteCombatHitMessage(
            TextWriter writer,
            CharacterTestCombatHitTraceEvent trace,
            char[] numberBuffer)
        {
            writer.Write("hitTrace hit target=");
            WriteInt64(writer, trace.TargetEntityId, numberBuffer);
            writer.Write(" kind=");
            WriteHitResolveKind(writer, trace.Kind, numberBuffer);
            writer.Write(" damageSource=");
            WriteEscapedTraceTextOrNone(writer, trace.DamageSourceId);
            writer.Write(" damage=");
            WriteInt64(writer, trace.Damage, numberBuffer);
            writer.Write(" hp=");
            if (trace.HasTarget)
            {
                WriteInt64(writer, trace.Health, numberBuffer);
                writer.Write('/');
                WriteInt64(writer, trace.MaxHealth, numberBuffer);
            }
            else
            {
                writer.Write("n/a");
            }

            writer.Write(" scatterPressure=");
            if (trace.HasTarget)
            {
                WriteInt64(writer, trace.ScatterPressure, numberBuffer);
                writer.Write('/');
                WriteInt64(writer, trace.MaxScatterPressure, numberBuffer);
                writer.Write('(');
                WritePressureBand(writer, trace.PressureBand, numberBuffer);
                writer.Write(')');
            }
            else
            {
                writer.Write("n/a");
            }

            writer.Write(" stagger=");
            if (trace.HasTarget)
                WriteInt64(writer, trace.StaggerFrames, numberBuffer);
            else
                writer.Write("n/a");
            writer.Write(" hitPoint=");
            WriteFixVector3(writer, trace.HitPointWorld, numberBuffer);
            writer.Write(" hitNormal=");
            WriteFixVector3(writer, trace.HitNormalWorld, numberBuffer);
            writer.Write(" trace=");
            WriteInt64(writer, trace.TraceId, numberBuffer);
        }

        private static void AppendCombatHitQueryMessage(
            StringBuilder builder,
            CharacterTestCombatHitQueryTraceEvent trace)
        {
            builder.Append("hitTrace query active=");
            builder.Append(trace.ActiveTraceCount);
            builder.Append(" candidates=");
            builder.Append(trace.CandidateCount);
            builder.Append(" resolved=");
            builder.Append(trace.ResolvedCount);
        }

        private static void WriteCombatHitQueryMessage(
            TextWriter writer,
            CharacterTestCombatHitQueryTraceEvent trace,
            char[] numberBuffer)
        {
            writer.Write("hitTrace query active=");
            WriteInt64(writer, trace.ActiveTraceCount, numberBuffer);
            writer.Write(" candidates=");
            WriteInt64(writer, trace.CandidateCount, numberBuffer);
            writer.Write(" resolved=");
            WriteInt64(writer, trace.ResolvedCount, numberBuffer);
        }

        private static void AppendScatterPressureMessage(
            StringBuilder builder,
            CharacterTestScatterPressureTraceEvent trace)
        {
            builder.Append("scatterPressure target=");
            builder.Append(trace.TargetEntityId);
            builder.Append(" delta=");
            builder.Append(trace.Delta);
            builder.Append(" impactPower=");
            builder.Append(trace.ImpactPower);
            builder.Append(" effective=");
            builder.Append(trace.EffectiveImpact.ToString("F2", CultureInfo.InvariantCulture));
            builder.Append(" mass=");
            builder.Append(trace.SourceMassKg.ToString("F0", CultureInfo.InvariantCulture));
            builder.Append("->");
            builder.Append(trace.TargetMassKg.ToString("F0", CultureInfo.InvariantCulture));
            builder.Append(" knockbackSpeed=");
            builder.Append(trace.KnockbackSpeed.ToString("F2", CultureInfo.InvariantCulture));
            builder.Append(" previewDistance=");
            builder.Append(trace.PreviewDistanceMeters.ToString("F2", CultureInfo.InvariantCulture));
            builder.Append(" value=");
            if (trace.HasPressureState)
            {
                builder.Append(trace.ScatterPressure);
                builder.Append('/');
                builder.Append(trace.CohesionLimit);
            }
            else
            {
                builder.Append("n/a");
            }

            builder.Append(" band=");
            if (trace.HasPressureState)
                AppendPressureBand(builder, trace.PressureBand);
            else
                builder.Append("n/a");
            builder.Append(" damage=");
            AppendTraceTextOrNone(builder, trace.DamageId);
            builder.Append(" trace=");
            builder.Append(trace.TraceId);
        }

        private static void WriteScatterPressureMessage(
            TextWriter writer,
            CharacterTestScatterPressureTraceEvent trace,
            char[] numberBuffer)
        {
            writer.Write("scatterPressure target=");
            WriteInt64(writer, trace.TargetEntityId, numberBuffer);
            writer.Write(" delta=");
            WriteInt64(writer, trace.Delta, numberBuffer);
            writer.Write(" impactPower=");
            WriteInt64(writer, trace.ImpactPower, numberBuffer);
            writer.Write(" effective=");
            WriteFixedFloat(writer, trace.EffectiveImpact, 2, numberBuffer);
            writer.Write(" mass=");
            WriteFixedFloat(writer, trace.SourceMassKg, 0, numberBuffer);
            writer.Write("->");
            WriteFixedFloat(writer, trace.TargetMassKg, 0, numberBuffer);
            writer.Write(" knockbackSpeed=");
            WriteFixedFloat(writer, trace.KnockbackSpeed, 2, numberBuffer);
            writer.Write(" previewDistance=");
            WriteFixedFloat(writer, trace.PreviewDistanceMeters, 2, numberBuffer);
            writer.Write(" value=");
            if (trace.HasPressureState)
            {
                WriteInt64(writer, trace.ScatterPressure, numberBuffer);
                writer.Write('/');
                WriteInt64(writer, trace.CohesionLimit, numberBuffer);
            }
            else
            {
                writer.Write("n/a");
            }

            writer.Write(" band=");
            if (trace.HasPressureState)
                WritePressureBand(writer, trace.PressureBand, numberBuffer);
            else
                writer.Write("n/a");
            writer.Write(" damage=");
            WriteEscapedTraceTextOrNone(writer, trace.DamageId);
            writer.Write(" trace=");
            WriteInt64(writer, trace.TraceId, numberBuffer);
        }

        private static void AppendCohesionBreakMessage(
            StringBuilder builder,
            CharacterTestCohesionBreakTraceEvent trace)
        {
            builder.Append("cohesionBreak target=");
            builder.Append(trace.TargetEntityId);
            builder.Append(" value=");
            builder.Append(trace.CurrentScatterPressure);
            builder.Append('/');
            builder.Append(trace.CurrentCohesionLimit);
            builder.Append(" delta=");
            builder.Append(trace.Delta);
            builder.Append(" trace=");
            AppendTraceTextOrNone(builder, trace.TraceId);
        }

        private static void WriteCohesionBreakMessage(
            TextWriter writer,
            CharacterTestCohesionBreakTraceEvent trace,
            char[] numberBuffer)
        {
            writer.Write("cohesionBreak target=");
            WriteInt64(writer, trace.TargetEntityId, numberBuffer);
            writer.Write(" value=");
            WriteInt64(writer, trace.CurrentScatterPressure, numberBuffer);
            writer.Write('/');
            WriteInt64(writer, trace.CurrentCohesionLimit, numberBuffer);
            writer.Write(" delta=");
            WriteInt64(writer, trace.Delta, numberBuffer);
            writer.Write(" trace=");
            WriteEscapedTraceTextOrNone(writer, trace.TraceId);
        }

        private static void AppendMotionControlScaleMessage(
            StringBuilder builder,
            CharacterTestMotionControlScaleTraceEvent trace)
        {
            builder.Append("controlScale move=");
            AppendMilliScale(builder, trace.MovementControlScaleMilli);
            builder.Append(" rot=");
            AppendMilliScale(builder, trace.RotationControlScaleMilli);
            builder.Append(" source=");
            AppendMotionRequestSource(builder, trace.Source);
        }

        private static void WriteMotionControlScaleMessage(
            TextWriter writer,
            CharacterTestMotionControlScaleTraceEvent trace,
            char[] numberBuffer)
        {
            writer.Write("controlScale move=");
            WriteMilliScale(writer, trace.MovementControlScaleMilli, 3, numberBuffer);
            writer.Write(" rot=");
            WriteMilliScale(writer, trace.RotationControlScaleMilli, 3, numberBuffer);
            writer.Write(" source=");
            WriteMotionRequestSource(writer, trace.Source);
        }

        private static void AppendJumpMotionMessage(
            StringBuilder builder,
            CharacterTestJumpMotionTraceEvent trace)
        {
            builder.Append(trace.Blocked ? "blocked impulseY=" : "applied impulseY=");
            builder.Append((trace.ImpulseYMillimetersPerSecond / 1000f).ToString("F2", CultureInfo.InvariantCulture));
            builder.Append(" source=");
            AppendMotionRequestSource(builder, trace.Source);
            if (!trace.Blocked)
                return;

            builder.Append(" grounded=");
            builder.Append(trace.WasGrounded);
            builder.Append(" coyoteFrames=");
            builder.Append(trace.CoyoteFrames);
        }

        private static void WriteJumpMotionMessage(
            TextWriter writer,
            CharacterTestJumpMotionTraceEvent trace,
            char[] numberBuffer)
        {
            writer.Write(trace.Blocked ? "blocked impulseY=" : "applied impulseY=");
            WriteMilliAsUnit(writer, trace.ImpulseYMillimetersPerSecond, 2, numberBuffer);
            writer.Write(" source=");
            WriteMotionRequestSource(writer, trace.Source);
            if (!trace.Blocked)
                return;

            writer.Write(" grounded=");
            writer.Write(trace.WasGrounded ? "True" : "False");
            writer.Write(" coyoteFrames=");
            WriteInt64(writer, trace.CoyoteFrames, numberBuffer);
        }

        private static void AppendRootMotionDeltaMessage(
            StringBuilder builder,
            CharacterTestRootMotionDeltaTraceEvent trace)
        {
            builder.Append("rootMotionDeltaMm=(");
            builder.Append(trace.DeltaXMillimeters);
            builder.Append(',');
            builder.Append(trace.DeltaYMillimeters);
            builder.Append(',');
            builder.Append(trace.DeltaZMillimeters);
            builder.Append(") source=");
            AppendMotionRequestSource(builder, trace.Source);
        }

        private static void WriteRootMotionDeltaMessage(
            TextWriter writer,
            CharacterTestRootMotionDeltaTraceEvent trace,
            char[] numberBuffer)
        {
            writer.Write("rootMotionDeltaMm=(");
            WriteInt64(writer, trace.DeltaXMillimeters, numberBuffer);
            writer.Write(',');
            WriteInt64(writer, trace.DeltaYMillimeters, numberBuffer);
            writer.Write(',');
            WriteInt64(writer, trace.DeltaZMillimeters, numberBuffer);
            writer.Write(") source=");
            WriteMotionRequestSource(writer, trace.Source);
        }

        private static void AppendRootMotionActiveMessage(
            StringBuilder builder,
            CharacterTestRootMotionActiveTraceEvent trace)
        {
            builder.Append("rootMotion active action=");
            AppendTraceTextOrNone(builder, trace.ActionId);
            builder.Append(" clip=");
            AppendTraceTextOrNone(builder, trace.ClipKey);
            builder.Append(" frames=");
            builder.Append(trace.StartFrame);
            builder.Append('-');
            builder.Append(trace.EndFrame);
        }

        private static void WriteRootMotionActiveMessage(
            TextWriter writer,
            CharacterTestRootMotionActiveTraceEvent trace,
            char[] numberBuffer)
        {
            writer.Write("rootMotion active action=");
            WriteEscapedTraceTextOrNone(writer, trace.ActionId);
            writer.Write(" clip=");
            WriteEscapedTraceTextOrNone(writer, trace.ClipKey);
            writer.Write(" frames=");
            WriteInt64(writer, trace.StartFrame, numberBuffer);
            writer.Write('-');
            WriteInt64(writer, trace.EndFrame, numberBuffer);
        }

        private static void AppendCollisionProfileBlockedMessage(
            StringBuilder builder,
            CharacterTestCollisionProfileBlockedTraceEvent trace)
        {
            builder.Append("collisionProfile blocked profile=");
            builder.Append(trace.ProfileId);
            builder.Append(" keep=");
            builder.Append(trace.KeepProfileId);
            builder.Append(" hits=");
            builder.Append(trace.HitCount);
        }

        private static void WriteCollisionProfileBlockedMessage(
            TextWriter writer,
            CharacterTestCollisionProfileBlockedTraceEvent trace,
            char[] numberBuffer)
        {
            writer.Write("collisionProfile blocked profile=");
            WriteInt64(writer, trace.ProfileId, numberBuffer);
            writer.Write(" keep=");
            WriteInt64(writer, trace.KeepProfileId, numberBuffer);
            writer.Write(" hits=");
            WriteInt64(writer, trace.HitCount, numberBuffer);
        }

        private static void AppendActionInputMessage(
            StringBuilder builder,
            CharacterTestActionInputTraceEvent trace)
        {
            AppendActionInputPrefix(builder, trace);
            if (trace.Kind == CharacterTestActionInputTraceKind.Jump)
            {
                builder.Append(" grounded=");
                builder.Append(trace.Grounded);
                builder.Append(" coyote=");
                builder.Append(CharacterTestDeterministicUnits.FramesToSeconds(trace.CoyoteFrames).ToString("F2", CultureInfo.InvariantCulture));
                builder.Append(" buffer=");
                builder.Append(CharacterTestDeterministicUnits.FramesToSeconds(trace.BufferFrames).ToString("F2", CultureInfo.InvariantCulture));
                return;
            }

            builder.Append(" active=");
            AppendTraceTextOrNone(builder, trace.ActiveActionId);
            builder.Append(" state=");
            AppendTraceTextOrNone(builder, CharacterTestCharacterActionComponent.ResolveStateText(trace.State));
        }

        private static void WriteActionInputMessage(
            TextWriter writer,
            CharacterTestActionInputTraceEvent trace,
            char[] numberBuffer)
        {
            WriteActionInputPrefix(writer, trace, numberBuffer);
            if (trace.Kind == CharacterTestActionInputTraceKind.Jump)
            {
                writer.Write(" grounded=");
                writer.Write(trace.Grounded ? "True" : "False");
                writer.Write(" coyote=");
                WriteFixedFloat(
                    writer,
                    CharacterTestDeterministicUnits.FramesToSeconds(trace.CoyoteFrames),
                    2,
                    numberBuffer);
                writer.Write(" buffer=");
                WriteFixedFloat(
                    writer,
                    CharacterTestDeterministicUnits.FramesToSeconds(trace.BufferFrames),
                    2,
                    numberBuffer);
                return;
            }

            writer.Write(" active=");
            WriteEscapedTraceTextOrNone(writer, trace.ActiveActionId);
            writer.Write(" state=");
            WriteEscapedTraceTextOrNone(writer, CharacterTestCharacterActionComponent.ResolveStateText(trace.State));
        }

        private static void AppendActionOutcomeMessage(
            StringBuilder builder,
            CharacterTestActionOutcomeTraceEvent trace)
        {
            AppendActionOutcomePrefix(builder, trace);
            switch (trace.Kind)
            {
                case CharacterTestActionOutcomeTraceKind.Accepted:
                case CharacterTestActionOutcomeTraceKind.AcceptedNoAnimation:
                case CharacterTestActionOutcomeTraceKind.DodgeAccepted:
                    builder.Append("accepted action=");
                    AppendTraceTextOrNone(builder, trace.ActionId);
                    if (trace.Kind == CharacterTestActionOutcomeTraceKind.Accepted)
                    {
                        builder.Append(" animKey=");
                        AppendTraceTextOrNone(builder, trace.AnimationKey);
                    }
                    break;
                case CharacterTestActionOutcomeTraceKind.JumpDeferred:
                    builder.Append("deferred waiting for grounded/coyote");
                    break;
                default:
                    builder.Append(trace.Kind == CharacterTestActionOutcomeTraceKind.AbilityRejected
                        ? "ability rejected reason="
                        : "rejected reason=");
                    AppendTraceTextOrNone(builder, trace.Reason);
                    if (trace.RemainingCooldownFrames >= 0)
                    {
                        builder.Append(" remainingCooldownFrames=");
                        builder.Append(trace.RemainingCooldownFrames);
                    }
                    if (trace.AbilityId != 0)
                    {
                        builder.Append(" abilityId=");
                        builder.Append(trace.AbilityId);
                    }
                    break;
            }
        }

        private static void WriteActionOutcomeMessage(
            TextWriter writer,
            CharacterTestActionOutcomeTraceEvent trace,
            char[] numberBuffer)
        {
            WriteActionOutcomePrefix(writer, trace, numberBuffer);
            switch (trace.Kind)
            {
                case CharacterTestActionOutcomeTraceKind.Accepted:
                case CharacterTestActionOutcomeTraceKind.AcceptedNoAnimation:
                case CharacterTestActionOutcomeTraceKind.DodgeAccepted:
                    writer.Write("accepted action=");
                    WriteEscapedTraceTextOrNone(writer, trace.ActionId);
                    if (trace.Kind == CharacterTestActionOutcomeTraceKind.Accepted)
                    {
                        writer.Write(" animKey=");
                        WriteEscapedTraceTextOrNone(writer, trace.AnimationKey);
                    }
                    break;
                case CharacterTestActionOutcomeTraceKind.JumpDeferred:
                    writer.Write("deferred waiting for grounded/coyote");
                    break;
                default:
                    writer.Write(trace.Kind == CharacterTestActionOutcomeTraceKind.AbilityRejected
                        ? "ability rejected reason="
                        : "rejected reason=");
                    WriteEscapedTraceTextOrNone(writer, trace.Reason);
                    if (trace.RemainingCooldownFrames >= 0)
                    {
                        writer.Write(" remainingCooldownFrames=");
                        WriteInt64(writer, trace.RemainingCooldownFrames, numberBuffer);
                    }
                    if (trace.AbilityId != 0)
                    {
                        writer.Write(" abilityId=");
                        WriteInt64(writer, trace.AbilityId, numberBuffer);
                    }
                    break;
            }
        }

        private static void AppendActionInputPrefix(StringBuilder builder, CharacterTestActionInputTraceEvent trace)
        {
            switch (trace.Kind)
            {
                case CharacterTestActionInputTraceKind.Fireball:
                    builder.Append("fireball pressed");
                    break;
                case CharacterTestActionInputTraceKind.FlameLance:
                    builder.Append("flame_lance pressed");
                    break;
                case CharacterTestActionInputTraceKind.FireballLob:
                    builder.Append("fireball_lob pressed");
                    break;
                case CharacterTestActionInputTraceKind.FireballBurst:
                    builder.Append("fireball_burst pressed");
                    break;
                case CharacterTestActionInputTraceKind.SkillSlot:
                    builder.Append("skill_slot ");
                    AppendSkillSlotLabel(builder, trace.SlotIndex);
                    builder.Append(" pressed");
                    break;
                case CharacterTestActionInputTraceKind.AbilitySlot:
                    builder.Append("ability_slot pressed slot=");
                    builder.Append(trace.AbilitySlot);
                    builder.Append(" abilityId=");
                    builder.Append(trace.AbilityId);
                    break;
                case CharacterTestActionInputTraceKind.Dodge:
                    builder.Append("dodge pressed");
                    break;
                default:
                    builder.Append("pressed");
                    break;
            }
        }

        private static void WriteActionInputPrefix(
            TextWriter writer,
            CharacterTestActionInputTraceEvent trace,
            char[] numberBuffer)
        {
            switch (trace.Kind)
            {
                case CharacterTestActionInputTraceKind.Fireball:
                    writer.Write("fireball pressed");
                    break;
                case CharacterTestActionInputTraceKind.FlameLance:
                    writer.Write("flame_lance pressed");
                    break;
                case CharacterTestActionInputTraceKind.FireballLob:
                    writer.Write("fireball_lob pressed");
                    break;
                case CharacterTestActionInputTraceKind.FireballBurst:
                    writer.Write("fireball_burst pressed");
                    break;
                case CharacterTestActionInputTraceKind.SkillSlot:
                    writer.Write("skill_slot ");
                    WriteSkillSlotLabel(writer, trace.SlotIndex, numberBuffer);
                    writer.Write(" pressed");
                    break;
                case CharacterTestActionInputTraceKind.AbilitySlot:
                    writer.Write("ability_slot pressed slot=");
                    WriteInt64(writer, trace.AbilitySlot, numberBuffer);
                    writer.Write(" abilityId=");
                    WriteInt64(writer, trace.AbilityId, numberBuffer);
                    break;
                case CharacterTestActionInputTraceKind.Dodge:
                    writer.Write("dodge pressed");
                    break;
                default:
                    writer.Write("pressed");
                    break;
            }
        }

        private static void AppendActionOutcomePrefix(StringBuilder builder, CharacterTestActionOutcomeTraceEvent trace)
        {
            if (trace.Kind == CharacterTestActionOutcomeTraceKind.DodgeAccepted
                || trace.Kind == CharacterTestActionOutcomeTraceKind.DodgeRejected)
            {
                builder.Append("dodge ");
                return;
            }

            if (trace.Kind == CharacterTestActionOutcomeTraceKind.SkillSlotRejected)
            {
                builder.Append("skill_slot ");
                if (trace.SlotIndex >= 0)
                {
                    AppendSkillSlotLabel(builder, trace.SlotIndex);
                    builder.Append(' ');
                }
                return;
            }

            if (!trace.ActionTrace.IsNone)
                builder.Append("trace(").Append(trace.ActionTrace.KindId).Append(':').Append(trace.ActionTrace.SourceId)
                    .Append(':').Append(trace.ActionTrace.ExecutionId).Append(':').Append(trace.ActionTrace.Sequence).Append(") ");
        }

        private static void WriteActionOutcomePrefix(
            TextWriter writer,
            CharacterTestActionOutcomeTraceEvent trace,
            char[] numberBuffer)
        {
            if (trace.Kind == CharacterTestActionOutcomeTraceKind.DodgeAccepted
                || trace.Kind == CharacterTestActionOutcomeTraceKind.DodgeRejected)
            {
                writer.Write("dodge ");
                return;
            }

            if (trace.Kind == CharacterTestActionOutcomeTraceKind.SkillSlotRejected)
            {
                writer.Write("skill_slot ");
                if (trace.SlotIndex >= 0)
                {
                    WriteSkillSlotLabel(writer, trace.SlotIndex, numberBuffer);
                    writer.Write(' ');
                }
                return;
            }

            if (!trace.ActionTrace.IsNone)
            {
                writer.Write("trace("); WriteInt64(writer, trace.ActionTrace.KindId, numberBuffer); writer.Write(':');
                WriteInt64(writer, trace.ActionTrace.SourceId, numberBuffer); writer.Write(':');
                WriteInt64(writer, trace.ActionTrace.ExecutionId, numberBuffer); writer.Write(':');
                WriteInt64(writer, trace.ActionTrace.Sequence, numberBuffer); writer.Write(") ");
            }
        }

        private static void AppendSkillSlotLabel(StringBuilder builder, int slotIndex)
        {
            if (slotIndex >= 0)
                builder.Append(slotIndex + 1);
            else
                builder.Append("none");
        }

        private static void WriteSkillSlotLabel(TextWriter writer, int slotIndex, char[] numberBuffer)
        {
            if (slotIndex >= 0)
                WriteInt64(writer, slotIndex + 1, numberBuffer);
            else
                writer.Write("none");
        }

        private static void AppendMilliScale(StringBuilder builder, int milli)
        {
            builder.Append((milli / 1000f).ToString("0.###", CultureInfo.InvariantCulture));
        }

        private static void WriteMilliScale(TextWriter writer, int milli, int maxDecimals, char[] numberBuffer)
        {
            int whole = milli / 1000;
            int fraction = Math.Abs(milli % 1000);
            if (milli < 0 && whole == 0)
                writer.Write("-0");
            else
                WriteInt64(writer, whole, numberBuffer);

            if (maxDecimals <= 0 || fraction == 0)
                return;

            int decimals = Math.Min(maxDecimals, 3);
            while (decimals > 0 && fraction % 10 == 0)
            {
                fraction /= 10;
                decimals--;
            }

            if (decimals <= 0)
                return;

            writer.Write('.');
            WritePaddedNumber(writer, fraction, decimals, numberBuffer);
        }

        private static void WriteMilliAsUnit(TextWriter writer, int milli, int decimals, char[] numberBuffer)
        {
            WriteFixedFloat(writer, milli / 1000f, decimals, numberBuffer);
        }

        private static void AppendFixVector3(StringBuilder builder, FixVector3 value)
        {
            builder.Append('(');
            AppendFix64(builder, value.X);
            builder.Append(", ");
            AppendFix64(builder, value.Y);
            builder.Append(", ");
            AppendFix64(builder, value.Z);
            builder.Append(')');
        }

        private static void WriteFixVector3(TextWriter writer, FixVector3 value, char[] numberBuffer)
        {
            writer.Write('(');
            WriteFix64(writer, value.X, numberBuffer);
            writer.Write(", ");
            WriteFix64(writer, value.Y, numberBuffer);
            writer.Write(", ");
            WriteFix64(writer, value.Z, numberBuffer);
            writer.Write(')');
        }

        private static void AppendFix64(StringBuilder builder, Fix64 value)
        {
            long whole = value.RawValue / Fix64.Scale;
            long fraction = value.RawValue % Fix64.Scale;
            if (fraction < 0)
                fraction = -fraction;

            if (value.RawValue < 0 && whole == 0)
                builder.Append("-0.");
            else
            {
                builder.Append(whole);
                builder.Append('.');
            }

            AppendSixDigitFraction(builder, fraction);
        }

        private static void WriteFix64(TextWriter writer, Fix64 value, char[] numberBuffer)
        {
            long whole = value.RawValue / Fix64.Scale;
            long fraction = value.RawValue % Fix64.Scale;
            if (fraction < 0)
                fraction = -fraction;

            if (value.RawValue < 0 && whole == 0)
                writer.Write("-0.");
            else
            {
                WriteInt64(writer, whole, numberBuffer);
                writer.Write('.');
            }

            WritePaddedNumber(writer, fraction, 6, numberBuffer);
        }

        private static void AppendSixDigitFraction(StringBuilder builder, long fraction)
        {
            if (fraction < 100000)
                builder.Append('0');
            if (fraction < 10000)
                builder.Append('0');
            if (fraction < 1000)
                builder.Append('0');
            if (fraction < 100)
                builder.Append('0');
            if (fraction < 10)
                builder.Append('0');
            builder.Append(fraction);
        }

        private static void WriteFixedFloat(TextWriter writer, float value, int decimals, char[] numberBuffer)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = 0f;

            int scale = 1;
            for (int i = 0; i < decimals; i++)
                scale *= 10;

            long scaled = (long)Math.Round(value * scale, MidpointRounding.AwayFromZero);
            if (scaled < 0)
            {
                writer.Write('-');
                scaled = -scaled;
            }

            long whole = scale <= 1 ? scaled : scaled / scale;
            long fraction = scale <= 1 ? 0 : scaled % scale;
            WriteInt64(writer, whole, numberBuffer);
            if (decimals <= 0)
                return;

            writer.Write('.');
            WritePaddedNumber(writer, fraction, decimals, numberBuffer);
        }

        private static void WritePaddedNumber(TextWriter writer, long value, int digits, char[] numberBuffer)
        {
            int start = numberBuffer.Length;
            long current = value;
            for (int i = 0; i < digits; i++)
            {
                numberBuffer[--start] = (char)('0' + (int)(current % 10));
                current /= 10;
            }

            writer.Write(numberBuffer, start, digits);
        }

        private static void WriteInt64(TextWriter writer, long value, char[] numberBuffer)
        {
            if (value == 0)
            {
                writer.Write('0');
                return;
            }

            ulong current;
            if (value < 0)
            {
                writer.Write('-');
                current = (ulong)(-(value + 1)) + 1UL;
            }
            else
            {
                current = (ulong)value;
            }

            int start = numberBuffer.Length;
            while (current > 0)
            {
                numberBuffer[--start] = (char)('0' + (int)(current % 10));
                current /= 10;
            }

            writer.Write(numberBuffer, start, numberBuffer.Length - start);
        }

        private static void AppendHitResolveKind(StringBuilder builder, HitResolveKind kind)
        {
            switch (kind)
            {
                case HitResolveKind.Damage:
                    builder.Append("Damage");
                    break;
                case HitResolveKind.TargetDead:
                    builder.Append("TargetDead");
                    break;
                case HitResolveKind.Invincible:
                    builder.Append("Invincible");
                    break;
                case HitResolveKind.Parried:
                    builder.Append("Parried");
                    break;
                case HitResolveKind.Blocked:
                    builder.Append("Blocked");
                    break;
                case HitResolveKind.Duplicate:
                    builder.Append("Duplicate");
                    break;
                case HitResolveKind.SelfDamage:
                    builder.Append("SelfDamage");
                    break;
                case HitResolveKind.Friendly:
                    builder.Append("Friendly");
                    break;
                default:
                    builder.Append((int)kind);
                    break;
            }
        }

        private static void WriteHitResolveKind(TextWriter writer, HitResolveKind kind, char[] numberBuffer)
        {
            switch (kind)
            {
                case HitResolveKind.Damage:
                    writer.Write("Damage");
                    break;
                case HitResolveKind.TargetDead:
                    writer.Write("TargetDead");
                    break;
                case HitResolveKind.Invincible:
                    writer.Write("Invincible");
                    break;
                case HitResolveKind.Parried:
                    writer.Write("Parried");
                    break;
                case HitResolveKind.Blocked:
                    writer.Write("Blocked");
                    break;
                case HitResolveKind.Duplicate:
                    writer.Write("Duplicate");
                    break;
                case HitResolveKind.SelfDamage:
                    writer.Write("SelfDamage");
                    break;
                case HitResolveKind.Friendly:
                    writer.Write("Friendly");
                    break;
                default:
                    WriteInt64(writer, (int)kind, numberBuffer);
                    break;
            }
        }

        private static void AppendPressureBand(StringBuilder builder, PressureBand band)
        {
            switch (band)
            {
                case PressureBand.Stable:
                    builder.Append("Stable");
                    break;
                case PressureBand.Pressed:
                    builder.Append("Pressed");
                    break;
                case PressureBand.Cracked:
                    builder.Append("Cracked");
                    break;
                case PressureBand.Critical:
                    builder.Append("Critical");
                    break;
                case PressureBand.Broken:
                    builder.Append("Broken");
                    break;
                default:
                    builder.Append((int)band);
                    break;
            }
        }

        private static void WritePressureBand(TextWriter writer, PressureBand band, char[] numberBuffer)
        {
            switch (band)
            {
                case PressureBand.Stable:
                    writer.Write("Stable");
                    break;
                case PressureBand.Pressed:
                    writer.Write("Pressed");
                    break;
                case PressureBand.Cracked:
                    writer.Write("Cracked");
                    break;
                case PressureBand.Critical:
                    writer.Write("Critical");
                    break;
                case PressureBand.Broken:
                    writer.Write("Broken");
                    break;
                default:
                    WriteInt64(writer, (int)band, numberBuffer);
                    break;
            }
        }

        private static void AppendTraceTextOrNone(StringBuilder builder, int value) => builder.Append(value);
        private static void WriteEscapedTraceTextOrNone(TextWriter writer, int value) => writer.Write(value);
        private static void AppendTraceTextOrNone(StringBuilder builder, RuntimeTraceIdentity value) => builder.Append(value.ToString());
        private static void WriteEscapedTraceTextOrNone(TextWriter writer, RuntimeTraceIdentity value) => writer.Write(value.ToString());

        private static void AppendTraceTextOrNone(StringBuilder builder, string value)
        {
            builder.Append(string.IsNullOrWhiteSpace(value) ? "none" : value);
        }

        private static void AppendTraceTextOrNone(StringBuilder builder, CharacterActionId value)
        {
            builder.Append(value.IsValid ? value.ToString() : "none");
        }

        private static void WriteEscapedTraceTextOrNone(TextWriter writer, CharacterActionId value)
        {
            writer.Write(value.IsValid ? value.ToString() : "none");
        }

        private static void WriteEscapedTraceTextOrNone(TextWriter writer, string value)
        {
            WriteEscapedString(writer, string.IsNullOrWhiteSpace(value) ? "none" : value);
        }

        private static void AppendMotionRequestSource(StringBuilder builder, CharacterMotionRequestSource source)
        {
            if (source.IsEmpty)
            {
                builder.Append("none");
                return;
            }

            if (!source.SourceActionId.IsValid)
            {
                builder.Append(source.StableEventId);
                return;
            }

            if (source.StableEventId == 0)
            {
                builder.Append(source.SourceActionId.Value);
                return;
            }

            builder.Append(source.SourceActionId.Value);
            builder.Append(':');
            builder.Append(source.StableEventId);
        }

        private static void WriteMotionRequestSource(TextWriter writer, CharacterMotionRequestSource source)
        {
            if (source.IsEmpty)
            {
                writer.Write("none");
                return;
            }

            if (!source.SourceActionId.IsValid)
            {
                writer.Write(source.StableEventId);
                return;
            }

            if (source.StableEventId == 0)
            {
                writer.Write(source.SourceActionId.ToString());
                return;
            }

            writer.Write(source.SourceActionId.ToString());
            writer.Write(':');
            writer.Write(source.StableEventId);
        }

        private static void WriteEscapedString(TextWriter writer, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        writer.Write("\\\\");
                        break;
                    case '"':
                        writer.Write("\\\"");
                        break;
                    case '\r':
                        writer.Write("\\r");
                        break;
                    case '\n':
                        writer.Write("\\n");
                        break;
                    case '\t':
                        writer.Write("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            writer.Write("\\u00");
                            WriteHexNibble(writer, c >> 4);
                            WriteHexNibble(writer, c);
                        }
                        else
                        {
                            writer.Write(c);
                        }
                        break;
                }
            }
        }

        private static void WriteHexNibble(TextWriter writer, int value)
        {
            int nibble = value & 0xF;
            writer.Write((char)(nibble < 10 ? '0' + nibble : 'a' + nibble - 10));
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }

    internal enum CharacterTestTraceEventKind
    {
        Text = 0,
        CombatHit = 1,
        CombatHitQuery = 2,
        ScatterPressure = 3,
        CohesionBreak = 4,
        MotionControlScale = 5,
        JumpMotion = 6,
        RootMotionDelta = 7,
        RootMotionActive = 8,
        CollisionProfileBlocked = 9,
        ActionInput = 10,
        ActionOutcome = 11,
    }

    internal enum CharacterTestActionInputTraceKind
    {
        Attack = 0,
        Fireball = 1,
        FlameLance = 2,
        FireballLob = 3,
        FireballBurst = 4,
        SkillSlot = 5,
        AbilitySlot = 6,
        Jump = 7,
        Dodge = 8,
    }

    internal enum CharacterTestActionOutcomeTraceKind
    {
        Accepted = 0,
        Rejected = 1,
        DodgeAccepted = 2,
        DodgeRejected = 3,
        AbilityRejected = 4,
        SkillSlotRejected = 5,
        JumpDeferred = 6,
        AcceptedNoAnimation = 7,
    }

    internal readonly struct CharacterTestMotionControlScaleTraceEvent
    {
        public CharacterTestMotionControlScaleTraceEvent(
            int movementControlScaleMilli,
            int rotationControlScaleMilli,
            CharacterMotionRequestSource source)
        {
            MovementControlScaleMilli = movementControlScaleMilli;
            RotationControlScaleMilli = rotationControlScaleMilli;
            Source = source;
        }

        public int MovementControlScaleMilli { get; }
        public int RotationControlScaleMilli { get; }
        public CharacterMotionRequestSource Source { get; }
    }

    internal readonly struct CharacterTestJumpMotionTraceEvent
    {
        public CharacterTestJumpMotionTraceEvent(
            bool blocked,
            int impulseYMillimetersPerSecond,
            CharacterMotionRequestSource source,
            bool wasGrounded = false,
            int coyoteFrames = 0)
        {
            Blocked = blocked;
            ImpulseYMillimetersPerSecond = impulseYMillimetersPerSecond;
            Source = source;
            WasGrounded = wasGrounded;
            CoyoteFrames = coyoteFrames < 0 ? 0 : coyoteFrames;
        }

        public bool Blocked { get; }
        public int ImpulseYMillimetersPerSecond { get; }
        public CharacterMotionRequestSource Source { get; }
        public bool WasGrounded { get; }
        public int CoyoteFrames { get; }
    }

    internal readonly struct CharacterTestRootMotionDeltaTraceEvent
    {
        public CharacterTestRootMotionDeltaTraceEvent(
            int deltaXMillimeters,
            int deltaYMillimeters,
            int deltaZMillimeters,
            CharacterMotionRequestSource source)
        {
            DeltaXMillimeters = deltaXMillimeters;
            DeltaYMillimeters = deltaYMillimeters;
            DeltaZMillimeters = deltaZMillimeters;
            Source = source;
        }

        public int DeltaXMillimeters { get; }
        public int DeltaYMillimeters { get; }
        public int DeltaZMillimeters { get; }
        public CharacterMotionRequestSource Source { get; }
    }

    internal readonly struct CharacterTestRootMotionActiveTraceEvent
    {
        public CharacterTestRootMotionActiveTraceEvent(
            CharacterActionId actionId,
            string clipKey,
            int startFrame,
            int endFrame)
        {
            ActionId = actionId;
            ClipKey = clipKey ?? string.Empty;
            StartFrame = startFrame;
            EndFrame = endFrame;
        }

        public CharacterActionId ActionId { get; }
        public string ClipKey { get; }
        public int StartFrame { get; }
        public int EndFrame { get; }
    }

    internal readonly struct CharacterTestCollisionProfileBlockedTraceEvent
    {
        public CharacterTestCollisionProfileBlockedTraceEvent(
            int profileId,
            int keepProfileId,
            int hitCount)
        {
            ProfileId = profileId;
            KeepProfileId = keepProfileId;
            HitCount = hitCount < 0 ? 0 : hitCount;
        }

        public int ProfileId { get; }
        public int KeepProfileId { get; }
        public int HitCount { get; }
    }

    internal readonly struct CharacterTestActionInputTraceEvent
    {
        public CharacterTestActionInputTraceEvent(
            CharacterTestActionInputTraceKind kind,
            CharacterActionId activeActionId = default,
            CharacterTestActionStateId state = CharacterTestActionStateId.None,
            int slotIndex = -1,
            int abilitySlot = 0,
            int abilityId = 0,
            bool grounded = false,
            int coyoteFrames = 0,
            int bufferFrames = 0)
        {
            Kind = kind;
            ActiveActionId = activeActionId;
            State = state;
            SlotIndex = slotIndex;
            AbilitySlot = abilitySlot;
            AbilityId = abilityId;
            Grounded = grounded;
            CoyoteFrames = coyoteFrames < 0 ? 0 : coyoteFrames;
            BufferFrames = bufferFrames < 0 ? 0 : bufferFrames;
        }

        public CharacterTestActionInputTraceKind Kind { get; }
        public CharacterActionId ActiveActionId { get; }
        public CharacterTestActionStateId State { get; }
        public int SlotIndex { get; }
        public int AbilitySlot { get; }
        public int AbilityId { get; }
        public bool Grounded { get; }
        public int CoyoteFrames { get; }
        public int BufferFrames { get; }
    }

    internal readonly struct CharacterTestActionOutcomeTraceEvent
    {
        public CharacterTestActionOutcomeTraceEvent(
            CharacterTestActionOutcomeTraceKind kind,
            CharacterActionId actionId = default,
            string animationKey = "",
            string reason = "",
            RuntimeTraceIdentity actionTrace = default,
            long remainingCooldownFrames = -1,
            int abilityId = 0,
            int slotIndex = -1)
        {
            Kind = kind;
            ActionId = actionId;
            AnimationKey = animationKey ?? string.Empty;
            Reason = reason ?? string.Empty;
            ActionTrace = actionTrace;
            RemainingCooldownFrames = remainingCooldownFrames;
            AbilityId = abilityId;
            SlotIndex = slotIndex;
        }

        public CharacterTestActionOutcomeTraceKind Kind { get; }
        public CharacterActionId ActionId { get; }
        public string AnimationKey { get; }
        public string Reason { get; }
        public RuntimeTraceIdentity ActionTrace { get; }
        public long RemainingCooldownFrames { get; }
        public int AbilityId { get; }
        public int SlotIndex { get; }
        public bool Warning =>
            Kind != CharacterTestActionOutcomeTraceKind.Accepted
            && Kind != CharacterTestActionOutcomeTraceKind.AcceptedNoAnimation
            && Kind != CharacterTestActionOutcomeTraceKind.DodgeAccepted;
    }

    internal readonly struct CharacterTestCombatHitTraceEvent
    {
        public CharacterTestCombatHitTraceEvent(
            int targetEntityId,
            HitResolveKind kind,
            int damageSourceId,
            int damage,
            bool hasTarget,
            int health,
            int maxHealth,
            int scatterPressure,
            int maxScatterPressure,
            PressureBand pressureBand,
            int staggerFrames,
            FixVector3 hitPointWorld,
            FixVector3 hitNormalWorld,
            int traceId)
        {
            TargetEntityId = targetEntityId;
            Kind = kind;
            DamageSourceId = damageSourceId;
            Damage = damage;
            HasTarget = hasTarget;
            Health = health;
            MaxHealth = maxHealth;
            ScatterPressure = scatterPressure;
            MaxScatterPressure = maxScatterPressure;
            PressureBand = pressureBand;
            StaggerFrames = staggerFrames;
            HitPointWorld = hitPointWorld;
            HitNormalWorld = hitNormalWorld;
            TraceId = traceId;
        }

        public int TargetEntityId { get; }
        public HitResolveKind Kind { get; }
        public int DamageSourceId { get; }
        public int Damage { get; }
        public bool HasTarget { get; }
        public int Health { get; }
        public int MaxHealth { get; }
        public int ScatterPressure { get; }
        public int MaxScatterPressure { get; }
        public PressureBand PressureBand { get; }
        public int StaggerFrames { get; }
        public FixVector3 HitPointWorld { get; }
        public FixVector3 HitNormalWorld { get; }
        public int TraceId { get; }
    }

    internal readonly struct CharacterTestCombatHitQueryTraceEvent
    {
        public CharacterTestCombatHitQueryTraceEvent(int activeTraceCount, int candidateCount, int resolvedCount)
        {
            ActiveTraceCount = activeTraceCount < 0 ? 0 : activeTraceCount;
            CandidateCount = candidateCount < 0 ? 0 : candidateCount;
            ResolvedCount = resolvedCount < 0 ? 0 : resolvedCount;
        }

        public int ActiveTraceCount { get; }
        public int CandidateCount { get; }
        public int ResolvedCount { get; }
    }

    internal readonly struct CharacterTestScatterPressureTraceEvent
    {
        public CharacterTestScatterPressureTraceEvent(
            int targetEntityId,
            int delta,
            int impactPower,
            float effectiveImpact,
            float sourceMassKg,
            float targetMassKg,
            float knockbackSpeed,
            float previewDistanceMeters,
            bool hasPressureState,
            int scatterPressure,
            int cohesionLimit,
            PressureBand pressureBand,
            int damageId,
            int traceId)
        {
            TargetEntityId = targetEntityId;
            Delta = delta;
            ImpactPower = impactPower;
            EffectiveImpact = effectiveImpact;
            SourceMassKg = sourceMassKg;
            TargetMassKg = targetMassKg;
            KnockbackSpeed = knockbackSpeed;
            PreviewDistanceMeters = previewDistanceMeters;
            HasPressureState = hasPressureState;
            ScatterPressure = scatterPressure;
            CohesionLimit = cohesionLimit;
            PressureBand = pressureBand;
            DamageId = damageId;
            TraceId = traceId;
        }

        public int TargetEntityId { get; }
        public int Delta { get; }
        public int ImpactPower { get; }
        public float EffectiveImpact { get; }
        public float SourceMassKg { get; }
        public float TargetMassKg { get; }
        public float KnockbackSpeed { get; }
        public float PreviewDistanceMeters { get; }
        public bool HasPressureState { get; }
        public int ScatterPressure { get; }
        public int CohesionLimit { get; }
        public PressureBand PressureBand { get; }
        public int DamageId { get; }
        public int TraceId { get; }
    }

    internal readonly struct CharacterTestCohesionBreakTraceEvent
    {
        public CharacterTestCohesionBreakTraceEvent(
            int targetEntityId,
            int currentScatterPressure,
            int currentCohesionLimit,
            int delta,
            RuntimeTraceIdentity traceId)
        {
            TargetEntityId = targetEntityId;
            CurrentScatterPressure = currentScatterPressure;
            CurrentCohesionLimit = currentCohesionLimit;
            Delta = delta;
            TraceId = traceId;
        }

        public int TargetEntityId { get; }
        public int CurrentScatterPressure { get; }
        public int CurrentCohesionLimit { get; }
        public int Delta { get; }
        public RuntimeTraceIdentity TraceId { get; }
    }
}
