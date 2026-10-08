#!/bin/bash
# A/B for #834 on the Pi, second attempt: the load is a SECOND instance of the game (v0.3.6, Girandole, nocap, a
# 1600x900 window), which saturates the V3D; xdotool's window moves and four `yes` did not move the frame rate.
# Every process is under `timeout`; `wait` only ever waits for the hog.
#   A: v0.3.6 under a short hog  -> expected: the step on the first plateaued window under the floor (before)
#   B: branch under a short hog  -> expected: the "under the floor" line, no step, settles native when the hog ends
#   C: branch under a long hog   -> expected: the step once the stretch is 10 s long
S=/tmp/claude-1000/-home-rdt/c5ec9e80-d353-47c0-b91c-09917cb493cf/scratchpad
BEFORE=/home/rdt/Downloads/BS3D-v0.3.6-linux-arm64
AFTER=/home/rdt/.cache/bs3d-wt/sustain/GamePi/bin/Release/net10.0

stopall() { for p in $(pgrep -x BS3D); do kill $p 2>/dev/null; done; sleep 1; for p in $(pgrep -x BS3D); do kill -9 $p 2>/dev/null; done; }
trap stopall EXIT

run() { # name exe-dir hog-seconds total-seconds
  local name=$1 dir=$2 hogs=$3 total=$4
  rm -rf $S/ud-$name $S/ud-hog-$name; mkdir -p $S/ud-$name $S/ud-hog-$name
  echo "=== $name: $(basename $dir), hog $hogs s, run $total s  (t0=$(date +%T))"
  (cd $BEFORE && exec timeout $hogs ./BS3D userdata=$S/ud-hog-$name nointernet mute nosplash windowed width=1600 height=900 level=Girandole nocap nofps) > $S/hog-$name.log 2>&1 &
  HOG=$!
  sleep 3
  (cd $dir && exec timeout $total ./BS3D userdata=$S/ud-$name nointernet mute nosplash windowed width=1600 height=900 logfps) > $S/$name.log 2>&1 &
  GAME=$!
  wait $HOG; echo "hog ended at $(date +%T)"
  wait $GAME; echo "game ended at $(date +%T)"
  grep -n "^\[resolution\]" $S/$name.log
  grep "^\[fps\]" $S/$name.log | cut -c7-11 | tr '\n' ' '; echo
}

run A $BEFORE 17 30
run B $AFTER 17 30
run C $AFTER 34 42
echo "=== done"
