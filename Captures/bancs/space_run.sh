# Usage : sh Captures/bancs/space_run.sh "g jf jlf jd jld fr" nom_capture pas_total
cd "C:/Users/dev/Documents/blob project"
unity command editor_stop >/dev/null 2>&1
echo "$1" > Captures/bancs/space_set.txt
unity command eval_file Captures/bancs/space_set.cs 2>&1 | tail -1 | sed 's/.*"result":"\([^"]*\)".*/\1/'
unity command editor_play >/dev/null 2>&1
sleep 5
from=0
total=${3:-2400}
while [ $from -lt $total ]; do
  to=$((from + 300))
  echo "$from $to" > Captures/bancs/space_game.txt
  r=$(unity command eval_file Captures/bancs/space_game.cs 2>&1 | tail -1 | sed 's/.*"result":"\([^"]*\)".*/\1/')
  from=$to
done
echo "$r"
unity command eval 'Camera.main.aspect = 540f / 960f; return "a";' >/dev/null 2>&1
sleep 1
echo "$2" > Captures/bancs/shot.txt
unity command eval_file Captures/bancs/portrait.cs >/dev/null 2>&1
