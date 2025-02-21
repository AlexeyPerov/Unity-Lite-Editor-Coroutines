using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace LiteEditorCoroutines
{
    public class LiteEditorCoroutine
    {
        public static LiteEditorCoroutine Start(IEnumerator enumerator, EditorWindow window = null)
        {
            return new LiteEditorCoroutine(enumerator, window);
        }

        private readonly bool _hasOwner;
        private WeakReference _ownerReference;
        private IEnumerator _routine;
        private bool _isDone;

        private LiteEditorCoroutine(IEnumerator routine)
        {
            _routine = routine ?? throw new ArgumentNullException(nameof(routine));
            EditorApplication.update += OnUpdate;
        }
        
        private LiteEditorCoroutine(IEnumerator routine, object owner) : this(routine)
        {
            if (owner == null) return;
            _ownerReference = new WeakReference(owner);
            _hasOwner = true;
        }

        public void Stop()
        {
            EditorApplication.update -= OnUpdate;
            _routine = null;
            _ownerReference = null;
        }
        
        private void OnUpdate()
        {
            if (_hasOwner && _ownerReference is null or { IsAlive: false })
            {
                Stop();
                return;
            }

            ProcessCurrentStack();
            
            if (_isDone)
            {
                Stop();
            }
        }

        private void ProcessCurrentStack()
        {
            var processingStack = new Stack<IEnumerator>();
            var current = _routine;

            while (true)
            {
                processingStack.Push(current); // creating stack: _routine -> _routine.current -> _routine.current.current etc
                
                if (current.Current is IEnumerator next)
                    current = next;
                else
                    break;
            }

            while (processingStack.Count > 0)
            {
                var next = processingStack.Pop();

                if (next == null)
                    return;

                var result = MoveNext(next); // result == false - means that some enumerator has ended

                if (!result.HasValue || result.Value)
                    break; // will continue on a next update

                var isRoot = next == _routine;

                if (!isRoot) 
                    continue;
                
                _isDone = true; // if result == false in root - that means we are done with this Coroutine
                break;
            }
        }

        private static bool? MoveNext(IEnumerator enumerator)
        {
            var yield = enumerator.Current;

            if (yield == null)
                return enumerator.MoveNext();

            bool canMoveNext;

            switch (yield)
            {
                case LiteEditorCoroutine yieldRoutine:
                    canMoveNext = yieldRoutine._isDone;
                    break;
                case WaitForSeconds yieldWaitForSeconds:
                {
                    if (!yieldWaitForSeconds.TimeStarted.HasValue)
                        yieldWaitForSeconds.Start();

                    canMoveNext = yieldWaitForSeconds.TimeStarted + yieldWaitForSeconds.WaitTimeSeconds
                                  <= EditorApplication.timeSinceStartup;
                    break;
                }
                case AsyncOperation operation:
                    canMoveNext = operation.isDone;
                    break;
                default:
                    canMoveNext = true;
                    break;
            }

            if (canMoveNext)
                return enumerator.MoveNext();

            return null;
        }
        
        public class WaitForSeconds
        {
            public float WaitTimeSeconds { get; }
            public double? TimeStarted { get; private set; }

            public WaitForSeconds(float seconds)
            {
                WaitTimeSeconds = seconds;
            }

            public void Start()
            {
                if (TimeStarted.HasValue)
                    return;
            
                TimeStarted = EditorApplication.timeSinceStartup;
            }
        }
    }
}