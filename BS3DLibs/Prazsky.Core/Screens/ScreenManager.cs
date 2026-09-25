using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Screens
{
    /// <summary>
    /// A stack of <see cref="Screen"/>s and the traversal over it. Push a pause over a level and the level is
    /// still there underneath, still drawn, no longer updating; pop it and the level carries on without ever
    /// having been torn down and rebuilt — which is the whole reason a stack is worth having over a
    /// <c>switch</c> on an enum.
    /// <para>
    /// <b>Mutations are deferred, and that is not a nicety.</b> A screen pushes, pops or replaces from inside
    /// its own <see cref="Screen.Update"/> — pressing "Play" is exactly that — so applying it there would
    /// mutate the list being walked. The operations queue and are applied between frames, so the walk always
    /// sees one consistent stack, and a screen may safely queue several in one frame (pop itself and push its
    /// successor) in the order it asked for them.
    /// </para>
    /// <para>
    /// <b>And every queued operation decides against the stack it is applied to, never the one it was queued
    /// on</b> (#576). Operations queued earlier in the same frame run first, so the live stack at the call is
    /// the wrong question: <see cref="PopTo{T}"/> once checked it at the call and silently skipped a pop whose
    /// target's push was still pending, and a Replace that took off whatever was on top when it ran swallowed
    /// a page pushed over the screen that had asked for it.
    /// </para>
    /// <para>
    /// It knows nothing about Myra, this game, or how anything is drawn. It is fed a frame and routes it.
    /// </para>
    /// </summary>
    public sealed class ScreenManager
    {
        private readonly List<Screen> _stack = new();

        //Queued push/pop/replace, applied between frames — see the class doc for why they cannot be immediate.
        //PopTo's target type and Replace's outgoing screen ride in the entry rather than in fields of their
        //own: two of them queued in one frame would otherwise share the field and the second would silently
        //act on the first one's target.
        private readonly List<(Action Apply, Screen Screen, Screen Old, Type PopTo)> _pending = new();

        //Rebuilt per traversal rather than allocated per frame: Update and Draw both need a slice of the stack
        //and neither may hold the live list while a screen on it queues a mutation
        private readonly List<Screen> _walk = new();

        /// <summary>The screen the player is looking at and whose input is live, or null on an empty stack.</summary>
        public Screen Active => _stack.Count > 0 ? _stack[^1] : null;

        public int Count => _stack.Count;

        /// <summary>Whether any screen on the stack is of the given type. Cheap; the stack is a handful deep.</summary>
        public bool Contains<T>() where T : Screen
        {
            for (int i = 0; i < _stack.Count; i++) if (_stack[i] is T) return true;

            return false;
        }

        /// <summary>Finds the lowest screen of the given type, or null. The stack never holds many of anything.</summary>
        public T Find<T>() where T : Screen
        {
            for (int i = 0; i < _stack.Count; i++) if (_stack[i] is T found) return found;

            return null;
        }

        /// <summary>Puts a screen on top of whatever is already there.</summary>
        public void Push(Screen screen)
        {
            if (screen == null) throw new ArgumentNullException(nameof(screen));

            _pending.Add((Action.Push, screen, null, null));
        }

        /// <summary>Takes the top screen off. Does nothing on an empty stack.</summary>
        public void Pop() => _pending.Add((Action.Pop, null, null, null));

        /// <summary>
        /// Takes <paramref name="old"/> off the stack and puts <paramref name="next"/> in its place — at its
        /// index, as one operation — so a screen hands over to its successor without having to be the top one.
        /// The splash is the case: it replaces itself with the front end, and a page pushed over it in the
        /// meantime stays where it is, over the front end, rather than being taken off in the splash's place.
        /// <para>
        /// Resolved when it is applied. If <paramref name="old"/> has left the stack by then (popped, reset, or
        /// already replaced by an earlier request), nothing happens and a line says so: the screen that asked
        /// is gone, and with it the place its successor was to take.
        /// </para>
        /// </summary>
        public void Replace(Screen old, Screen next)
        {
            if (old == null) throw new ArgumentNullException(nameof(old));
            if (next == null) throw new ArgumentNullException(nameof(next));

            _pending.Add((Action.Replace, next, old, null));
        }

        /// <summary>
        /// Empties the stack. An empty manager is a legitimate resting state, not an error: a host may have
        /// nothing to show, and a game whose menus are screens before its play loop is one spends a level with
        /// nothing on the stack. Every screen it left is properly given its <see cref="Screen.Leave"/> on the
        /// way out.
        /// </summary>
        public void Clear() => _pending.Add((Action.Clear, null, null, null));

        /// <summary>
        /// Empties the stack and puts this screen on it — going back to the front end from anywhere, without
        /// having to know how deep the player wandered.
        /// </summary>
        public void Reset(Screen screen)
        {
            if (screen == null) throw new ArgumentNullException(nameof(screen));

            _pending.Add((Action.Reset, screen, null, null));
        }

        /// <summary>
        /// Pops screens until one of the given type is on top. Does nothing if there is none — so "back to the
        /// pause menu" from two panels deep is one call that cannot overshoot into an empty stack.
        /// <para>
        /// "There is none" is decided when the pop is <b>applied</b>, against the stack the operations queued
        /// before it have left — a push of the target queued in the same frame counts. Checked at the call, the
        /// <c>play</c> launch argument's pop to the backdrop found the backdrop's push still pending, skipped,
        /// and left the splash buried under the session.
        /// </para>
        /// </summary>
        public void PopTo<T>() where T : Screen => _pending.Add((Action.PopTo, null, null, typeof(T)));

        public void Update(GameTime gameTime)
        {
            ApplyPending();

            //Top down until something says the screens below it are frozen. Collected first: a screen updating
            //here is exactly where a push comes from, and the live stack must not be walked while it changes.
            _walk.Clear();

            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                _walk.Add(_stack[i]);

                if (!_stack[i].UpdatesUnderlying) break;
            }

            //Bottom-up within that slice, so the screen a push comes from has already had its say
            for (int i = _walk.Count - 1; i >= 0; i--) _walk[i].Update(gameTime);
        }

        public void Draw(GameTime gameTime)
        {
            //Down to the lowest screen that is not covered, then back up, so what is behind is painted first
            _walk.Clear();

            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                _walk.Add(_stack[i]);

                if (!_stack[i].DrawsUnderlying) break;
            }

            for (int i = _walk.Count - 1; i >= 0; i--) _walk[i].Draw(gameTime);
        }

        /// <summary>
        /// Applies everything queued since the last frame, in the order it was asked for. Run at the top of
        /// <see cref="Update"/> rather than at the bottom, so a screen pushed on one frame gets its
        /// <see cref="Screen.Enter"/> and its first <see cref="Screen.Update"/> before anything is drawn — a
        /// screen drawn before it has ever updated is one frame of whatever its fields happened to start at.
        /// </summary>
        private void ApplyPending()
        {
            if (_pending.Count == 0) return;

            //Copied out first: Enter() may queue further operations, and those belong to the NEXT frame rather
            //than to this loop, which would otherwise run away
            int count = _pending.Count;

            for (int i = 0; i < count; i++)
            {
                (Action action, Screen screen, Screen old, Type popTo) = _pending[i];

                switch (action)
                {
                    case Action.Push:
                        Add(screen);
                        break;

                    case Action.Pop:
                        RemoveTop();
                        break;

                    case Action.Replace:
                        int at = _stack.IndexOf(old);

                        if (at < 0)
                        {
                            Console.WriteLine($"[screens] Replace of {old.GetType().Name} by {screen.GetType().Name} ignored: it is no longer on the stack");
                            break;
                        }

                        RemoveAt(at);
                        Add(screen, at);
                        break;

                    case Action.Clear:
                        while (_stack.Count > 0) RemoveTop();
                        break;

                    case Action.Reset:
                        while (_stack.Count > 0) RemoveTop();
                        Add(screen);
                        break;

                    case Action.PopTo:
                        //Checked first: popping towards a type that is not there would empty the stack
                        if (!ContainsType(popTo))
                        {
                            Console.WriteLine($"[screens] PopTo<{popTo.Name}> ignored: none on the stack");
                            break;
                        }

                        while (!popTo.IsInstanceOfType(_stack[^1])) RemoveTop();
                        break;
                }
            }

            _pending.RemoveRange(0, count);

            //The screen now on top has a new neighbour above or below it either way
            Active?.CoveredChanged();
        }

        private bool ContainsType(Type type)
        {
            for (int i = 0; i < _stack.Count; i++) if (type.IsInstanceOfType(_stack[i])) return true;

            return false;
        }

        //On top unless an index says where: a Replace puts the successor where the screen it replaces stood
        private void Add(Screen screen, int at = -1)
        {
            screen.Manager = this;

            if (at < 0) _stack.Add(screen);
            else _stack.Insert(at, screen);

            screen.Enter();
        }

        private void RemoveTop()
        {
            if (_stack.Count > 0) RemoveAt(_stack.Count - 1);
        }

        private void RemoveAt(int index)
        {
            Screen screen = _stack[index];

            _stack.RemoveAt(index);

            screen.Leave();
            screen.Manager = null;
        }

        private enum Action { Push, Pop, Replace, Clear, Reset, PopTo }
    }
}
