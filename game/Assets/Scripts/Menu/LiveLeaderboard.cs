using System;
using System.Collections.Generic;
using MotionRunner.Progression;
using MotionRunner.Social;

namespace MotionRunner.Menu
{
    /// ILeaderboardSource over the online profile seam — the class MockLeaderboard's comment
    /// promised ("the Supabase board replaces the mock without the card knowing"). The seam is
    /// synchronous and the network is not, so this is a cache: Refresh() fetches, Changed fires
    /// when rows land, Top() serves whatever is cached. IsLive is false until the first rows
    /// arrive, and the card shows no rows at all until then (LeaderboardCard.ShowUnavailable).
    public sealed class LiveLeaderboard : ILeaderboardSource
    {
        readonly IProfileService _service;

        BoardResult _board;
        BoardQuery _query;
        bool _hasQuery;
        int _generation;

        /// Cached rows changed (a fetch landed, or the cache was cleared by a query change).
        public event Action Changed;

        public LiveLeaderboard(IProfileService service)
        {
            _service = service;
        }

        public bool IsLive => _board != null;
        public bool IsLoading { get; private set; }
        public SocialError LastError { get; private set; }

        /// The caller's rank on the cached board, 0 when unknown or unranked.
        public int MyRank => _board?.MyRank ?? 0;

        /// Point at a board. A different board drops the cache (stale rows from another board
        /// are worse than none) and fetches; the same board just refetches.
        public void Show(BoardQuery query)
        {
            bool sameBoard = _hasQuery && SameBoard(_query, query);
            _query = query;
            _hasQuery = true;
            if (!sameBoard && _board != null)
            {
                _board = null;
                Changed?.Invoke();
            }
            Refresh();
        }

        public void Refresh()
        {
            if (!_hasQuery || _service == null) return;

            // Stale-response guard: only the newest request may write the cache.
            int generation = ++_generation;
            IsLoading = true;
            LastError = null;
            Changed?.Invoke();
            _service.FetchBoard(_query, (board, error) =>
            {
                if (generation != _generation) return;
                IsLoading = false;
                LastError = error ?? (board == null ? new SocialError("unavailable", "no board") : null);
                if (LastError == null) _board = board;
                Changed?.Invoke();
            });
        }

        public IReadOnlyList<LeaderboardEntry> Top(int count, int yourScore)
        {
            if (_board == null || count < 1) return Array.Empty<LeaderboardEntry>();

            string myHandle = _service.Current?.Handle ?? string.Empty;
            var rows = new List<LeaderboardEntry>(count);
            bool placed = false;

            foreach (var row in _board.Rows)
            {
                if (rows.Count >= count) break;
                bool isYou = !placed && row.Handle == myHandle;
                placed |= isYou;
                rows.Add(new LeaderboardEntry(row.Rank, row.Handle, row.Score, isYou));
            }

            // The player is on the board but below the visible rows: the bottom row becomes
            // theirs, carrying the rank the server says they earned - the same non-contiguous
            // "..." contract MockLeaderboard keeps (pinned in LeaderboardTests). A player who
            // has never submitted (MyRank 0) gets no row: absence is the honest answer there.
            if (!placed && _board.MyRank > 0 && rows.Count > 0)
            {
                rows[rows.Count - 1] =
                    new LeaderboardEntry(_board.MyRank, myHandle, _board.MyScore, true);
            }

            return rows;
        }

        static bool SameBoard(in BoardQuery a, in BoardQuery b) =>
            a.Scope == b.Scope && a.DayLabel == b.DayLabel &&
            a.ContentVersion == b.ContentVersion && a.WorldId == b.WorldId &&
            a.InputGroup == b.InputGroup;
    }
}
