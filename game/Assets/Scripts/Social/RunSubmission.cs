using System;
using System.Globalization;

namespace MotionRunner.Social
{
    /// One finished run as the backend receives it. Field names are snake_case ON PURPOSE:
    /// the Supabase adapter serializes this class with JsonUtility, whose JSON keys are the
    /// field names — renaming a field here renames it on the wire and desyncs
    /// supabase/functions/_shared/validate.ts.
    [Serializable]
    public sealed class RunSubmission
    {
        public string client_run_id;
        public string mode;            // "daily" | "free"
        public int seed;
        public string content_version;
        public string world_id;
        public string day_label;       // Display-only "2026-09-09"; server derives the date from seed.
        public string input_mode;      // InputModes.Tilt | Camera | CameraFallback
        public int score;
        public float distance_m;
        public int coins;
        public int best_combo;
        public float duration_s;
        public string app_version;
        public string platform;        // "android_gp" | "android_galaxy" | "ios"

        public const string ModeDaily = "daily";
        public const string ModeFree = "free";
    }

    /// The input-mode vocabulary and the board-grouping rule, in one tested place.
    public static class InputModes
    {
        public const string Tilt = "tilt";
        public const string Camera = "camera";

        /// A run that STARTED on the camera and finished on the tilt fallback (CLAUDE.md
        /// rule 3). Owner call, 3 Sep: it competes on the standard board, never the camera
        /// board — nobody ranks hands-free with a half-thumbed run. Note the deliberate
        /// asymmetry with the LOCAL BestBoard, which scores such a run as the camera run the
        /// player chose to start (Feature D): the local board answers "what did I pick", the
        /// shared board answers "what was I actually steering with".
        public const string CameraFallback = "camera_fallback";

        public const string GroupStandard = "standard";
        public const string GroupCamera = "camera";

        public static string For(bool cameraScheme, bool cameraDropped)
        {
            if (!cameraScheme) return Tilt;
            return cameraDropped ? CameraFallback : Camera;
        }

        /// Mirrors the runs.input_group generated column server-side. The server recomputes it
        /// and never trusts this — the client copy exists only so the UI can show the board a
        /// run will land on.
        public static string GroupFor(string inputMode) =>
            inputMode == Camera ? GroupCamera : GroupStandard;
    }

    /// What SubmitRun completed as.
    public enum SubmitStatus
    {
        /// On the server; ranks below are meaningful for accepted Daily runs.
        Accepted,

        /// On the server but flagged (failed a plausibility bound) — kept in history, no board.
        NotAccepted,

        /// Same client_run_id was already processed — a retry, not a new run.
        Duplicate,

        /// Stored locally; will retry when a session and network exist.
        Queued,

        /// Rejected outright (schema, rate limit) — dropped.
        Rejected
    }

    public readonly struct SubmitOutcome
    {
        public readonly SubmitStatus Status;

        /// 1-based rank on today's board after this run, or 0 when unknown/not applicable.
        public readonly int DailyRank;

        /// 1-based rank on the all-time board, or 0.
        public readonly int AlltimeRank;

        public readonly SocialError Error;

        public SubmitOutcome(SubmitStatus status, int dailyRank = 0, int alltimeRank = 0,
            SocialError error = null)
        {
            Status = status;
            DailyRank = dailyRank;
            AlltimeRank = alltimeRank;
            Error = error;
        }
    }

    /// Which board to read.
    public enum BoardScope
    {
        Daily = 0,
        AllTime = 1
    }

    public readonly struct BoardQuery
    {
        public readonly BoardScope Scope;
        public readonly string DayLabel;        // ignored for AllTime
        public readonly string ContentVersion;
        public readonly string WorldId;
        public readonly string InputGroup;      // InputModes.GroupStandard | GroupCamera
        public readonly int Limit;

        public BoardQuery(BoardScope scope, string dayLabel, string contentVersion,
            string worldId, string inputGroup, int limit)
        {
            Scope = scope;
            DayLabel = dayLabel ?? string.Empty;
            ContentVersion = contentVersion ?? string.Empty;
            WorldId = worldId ?? string.Empty;
            InputGroup = inputGroup ?? InputModes.GroupStandard;
            Limit = limit;
        }
    }

    public readonly struct BoardRow
    {
        public readonly int Rank;
        public readonly string Handle;
        public readonly int Score;

        public BoardRow(int rank, string handle, int score)
        {
            Rank = rank;
            Handle = handle ?? string.Empty;
            Score = score;
        }
    }

    public sealed class BoardResult
    {
        public readonly System.Collections.Generic.IReadOnlyList<BoardRow> Rows;

        /// The caller's 1-based rank on this board, or 0 when they are not on it.
        public readonly int MyRank;

        /// The caller's score on this board, or 0.
        public readonly int MyScore;

        public BoardResult(System.Collections.Generic.IReadOnlyList<BoardRow> rows,
            int myRank, int myScore)
        {
            Rows = rows ?? Array.Empty<BoardRow>();
            MyRank = myRank;
            MyScore = myScore;
        }
    }

    /// Platform tag as the runs table expects it. One place, so a Galaxy build flavour later
    /// changes one constant path rather than a string in RunSession.
    public static class SubmissionPlatform
    {
        public const string AndroidGooglePlay = "android_gp";
        public const string AndroidGalaxy = "android_galaxy";
        public const string Ios = "ios";
    }

    /// Formatting helpers shared by the encoders. Invariant culture everywhere: "3,5" instead
    /// of "3.5" from a device locale would fail the server's schema check.
    public static class SocialFormat
    {
        public static string Number(float value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);

        public static bool TryNumber(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
