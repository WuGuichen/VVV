using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed class CharacterTestLevelEditorDocumentUndoState : ScriptableObject
    {
        [SerializeField]
        private string snapshot = string.Empty;

        [SerializeField]
        private int revision;

        public string Snapshot
        {
            get => snapshot ?? string.Empty;
            set => snapshot = value ?? string.Empty;
        }

        public int Revision
        {
            get => revision;
            set => revision = value;
        }
    }

    internal sealed class CharacterTestLevelEditorDocumentUndoMarker : MonoBehaviour
    {
        [SerializeField]
        private string snapshot = string.Empty;

        [SerializeField]
        private int revision;

        public string Snapshot
        {
            get => snapshot ?? string.Empty;
            set => snapshot = value ?? string.Empty;
        }

        public int Revision
        {
            get => revision;
            set => revision = value;
        }
    }

    internal sealed class CharacterTestLevelEditorDocumentUndoSession
    {
        internal sealed class Transaction : IDisposable
        {
            private CharacterTestLevelEditorDocumentUndoSession _owner;
            private readonly int _group;
            private readonly TransactionRecord _record;
            private readonly bool _collapseOnCommit;
            private bool _committed;

            internal Transaction(
                CharacterTestLevelEditorDocumentUndoSession owner,
                int group,
                TransactionRecord record,
                bool collapseOnCommit)
            {
                _owner = owner;
                _group = group;
                _record = record;
                _collapseOnCommit = collapseOnCommit;
            }

            public void Commit()
            {
                if (_owner == null || _committed)
                    return;

                _owner.CommitTransaction(_group, _record, _collapseOnCommit);
                _committed = true;
                _owner = null;
            }

            public void Dispose()
            {
                if (_owner == null)
                    return;

                _owner.AbortTransaction(_group, _record);
                _owner = null;
            }
        }

        internal sealed class TransactionRecord
        {
            public CharacterTestLevelEditorDocumentUndoMarker Marker;
            public string MarkerName;
            public string BeforeSnapshot;
            public string AfterSnapshot;
            public bool WasAlive;
        }

        private static readonly List<WeakReference<CharacterTestLevelEditorDocumentUndoSession>>
            Sessions =
                new List<WeakReference<CharacterTestLevelEditorDocumentUndoSession>>();
        private static bool _undoCallbackRegistered;

        private readonly string _displayName;
        private readonly Func<string> _captureSnapshot;
        private readonly Action<string> _restoreSnapshot;
        private readonly Action _refresh;
        private readonly CharacterTestLevelEditorDocumentUndoState _state;
        private readonly List<TransactionRecord> _transactions = new List<TransactionRecord>();
        private string _savedBaseline = string.Empty;
        private bool _transactionOpen;
        private bool _retired;
        private TransactionRecord _pendingTransaction;

        public CharacterTestLevelEditorDocumentUndoSession(
            string displayName,
            Func<string> captureSnapshot,
            Action<string> restoreSnapshot,
            Action refresh)
        {
            _displayName = string.IsNullOrWhiteSpace(displayName)
                ? "关卡编辑"
                : displayName.Trim();
            _captureSnapshot = captureSnapshot ?? throw new ArgumentNullException(nameof(captureSnapshot));
            _restoreSnapshot = restoreSnapshot ?? throw new ArgumentNullException(nameof(restoreSnapshot));
            _refresh = refresh;
            _state = ScriptableObject.CreateInstance<CharacterTestLevelEditorDocumentUndoState>();
            _state.name = _displayName + " Undo State";
            _state.hideFlags = HideFlags.HideAndDontSave;
            Sessions.Add(
                new WeakReference<CharacterTestLevelEditorDocumentUndoSession>(this));
            if (!_undoCallbackRegistered)
            {
                Undo.undoRedoPerformed += NotifyUndoRedoPerformed;
                _undoCallbackRegistered = true;
            }
        }

        internal void Retire()
        {
            if (_transactionOpen) throw new InvalidOperationException("活动的文档 Undo 事务尚未结束。");
            _retired = true;
        }

        internal string SavedBaseline => _savedBaseline;

        internal void SetSavedBaseline(string snapshot) => _savedBaseline = snapshot ?? string.Empty;

        public bool IsAtSavedBaseline =>
            string.Equals(CurrentSnapshot, _savedBaseline, StringComparison.Ordinal);

        private string CurrentSnapshot => _state == null ? string.Empty : _state.Snapshot;

        public void ResetBaseline()
        {
            string snapshot = _captureSnapshot() ?? string.Empty;
            _state.Snapshot = snapshot;
            _state.Revision++;
            _savedBaseline = snapshot;
            EditorUtility.SetDirty(_state);
        }

        public void SynchronizeCurrentDocument()
        {
            if (_transactionOpen)
                return;

            string snapshot = _captureSnapshot() ?? string.Empty;
            if (string.Equals(snapshot, CurrentSnapshot, StringComparison.Ordinal))
                return;

            _state.Snapshot = snapshot;
            _state.Revision++;
            EditorUtility.SetDirty(_state);
        }

        public Transaction BeginTransaction(string operationName)
        {
            if (_retired) throw new InvalidOperationException("此文档的 Undo 会话已退役，请重建文档会话。");
            SynchronizeCurrentDocument();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            return BeginTransactionCore(operationName, group, collapseOnCommit: true);
        }

        internal Transaction BeginJoinedTransaction(string operationName, int undoGroup)
        {
            if (_retired) throw new InvalidOperationException("此文档的 Undo 会话已退役，请重建文档会话。");
            if (undoGroup < 0)
                throw new ArgumentOutOfRangeException(nameof(undoGroup));
            SynchronizeCurrentDocument();
            return BeginTransactionCore(operationName, undoGroup, collapseOnCommit: false);
        }

        private Transaction BeginTransactionCore(
            string operationName,
            int group,
            bool collapseOnCommit)
        {
            string label = string.IsNullOrWhiteSpace(operationName)
                ? _displayName
                : operationName.Trim();
            Undo.SetCurrentGroupName(label);
            CharacterTestLevelEditorDocumentUndoMarker marker =
                CreateMarker("MxLevelEditorUndo:" + Guid.NewGuid().ToString("N"));
            marker.Snapshot = CurrentSnapshot;
            marker.Revision = _state.Revision + 1;
            _pendingTransaction = new TransactionRecord
            {
                Marker = marker,
                MarkerName = marker.name,
                BeforeSnapshot = CurrentSnapshot,
                AfterSnapshot = CurrentSnapshot,
                WasAlive = true
            };
            _transactions.Add(_pendingTransaction);
            Undo.RegisterCreatedObjectUndo(marker.gameObject, label);
            _transactionOpen = true;
            return new Transaction(this, group, _pendingTransaction, collapseOnCommit);
        }

        private void CommitTransaction(
            int group,
            TransactionRecord transaction,
            bool collapseOnCommit)
        {
            if (!_transactionOpen || transaction != _pendingTransaction)
                throw new InvalidOperationException("关卡编辑事务不是当前活动事务。");

            string snapshot = _captureSnapshot() ?? string.Empty;
            _state.Snapshot = snapshot;
            _state.Revision++;
            if (_pendingTransaction != null)
            {
                _pendingTransaction.AfterSnapshot = snapshot;
                if (_pendingTransaction.Marker != null)
                {
                    _pendingTransaction.Marker.Snapshot = snapshot;
                    _pendingTransaction.Marker.Revision = _state.Revision;
                    EditorUtility.SetDirty(_pendingTransaction.Marker);
                }
            }

            EditorUtility.SetDirty(_state);
            Undo.FlushUndoRecordObjects();
            if (collapseOnCommit)
                Undo.CollapseUndoOperations(group);
            _pendingTransaction = null;
            _transactionOpen = false;
        }

        private void AbortTransaction(int group, TransactionRecord transaction)
        {
            if (transaction != _pendingTransaction)
                return;

            try
            {
                Undo.RevertAllDownToGroup(group);
            }
            finally
            {
                _transactions.Remove(transaction);
                _pendingTransaction = null;
                _transactionOpen = false;
                _state.Snapshot = transaction.BeforeSnapshot ?? string.Empty;
                _state.Revision++;
                EditorUtility.SetDirty(_state);
                _restoreSnapshot(_state.Snapshot);
                _refresh?.Invoke();
            }
        }

        private void OnUndoRedoPerformed()
        {
            if (_retired || _state == null)
            {
                return;
            }

            CharacterTestLevelEditorDocumentUndoMarker[] activeMarkers =
                UnityEngine.Resources.FindObjectsOfTypeAll<
                    CharacterTestLevelEditorDocumentUndoMarker>();
            var markersByName =
                new Dictionary<string, CharacterTestLevelEditorDocumentUndoMarker>(
                    activeMarkers.Length,
                    StringComparer.Ordinal);
            for (int i = 0; i < activeMarkers.Length; i++)
            {
                CharacterTestLevelEditorDocumentUndoMarker marker = activeMarkers[i];
                if (marker != null && !string.IsNullOrWhiteSpace(marker.name))
                    markersByName[marker.name] = marker;
            }

            string snapshot = null;
            for (int i = 0; i < _transactions.Count; i++)
            {
                TransactionRecord transaction = _transactions[i];
                bool isAlive = transaction.Marker != null
                    || markersByName.TryGetValue(
                        transaction.MarkerName,
                        out transaction.Marker);
                if (isAlive == transaction.WasAlive)
                    continue;

                snapshot = isAlive
                    ? transaction.AfterSnapshot
                    : transaction.BeforeSnapshot;
                transaction.WasAlive = isAlive;
            }

            if (snapshot == null)
                return;

            _state.Snapshot = snapshot;
            _state.Revision++;
            EditorUtility.SetDirty(_state);
            _restoreSnapshot(snapshot);
            _refresh?.Invoke();
        }

        private static CharacterTestLevelEditorDocumentUndoMarker CreateMarker(string name)
        {
            var gameObject = new GameObject(name)
            {
                hideFlags = HideFlags.HideInHierarchy
                    | HideFlags.DontSaveInEditor
                    | HideFlags.DontSaveInBuild
            };
            CharacterTestLevelEditorDocumentUndoMarker marker =
                gameObject.AddComponent<CharacterTestLevelEditorDocumentUndoMarker>();
            marker.hideFlags = gameObject.hideFlags;
            return marker;
        }

        private static void NotifyUndoRedoPerformed()
        {
            for (int i = Sessions.Count - 1; i >= 0; i--)
            {
                if (!Sessions[i].TryGetTarget(
                        out CharacterTestLevelEditorDocumentUndoSession session))
                {
                    Sessions.RemoveAt(i);
                    continue;
                }

                session.OnUndoRedoPerformed();
            }
        }
    }
}
