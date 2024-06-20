#!/bin/bash

timestamp() {
  date +"%Y-%m-%d_%H-%M-%S"
}

size() {
  stat -f%z $1
}

VIPERFILE=$1
#echo ">>>>> VIPERFILE==$VIPERFILE"

FILENAME=$(basename $VIPERFILE)
#echo ">>>>> FILENAME==$FILENAME"

Z3LOGFILE="/tmp/$FILENAME--$(timestamp)"
#echo ">>>>> Z3LOGFILE==$Z3LOGFILE"

echo ">>>>> Writing Z3 logs with prefix $Z3LOGFILE ..."

Z3_EXE=/usr/local/Viper/z3/bin/z3

SILICON="java -Xmx2048m -Xss16m -cp :/usr/local/Viper/backends/* viper.silicon.SiliconRunner --numberOfParallelVerifiers=1 --z3Exe=$Z3_EXE --z3LogFile=$Z3LOGFILE --timeout=100 $VIPERFILE"

echo ">>>>> Running $SILICON"

eval $SILICON

IN_FILES=$(find /tmp/ -maxdepth 1 -name "$FILENAME*.smt2")

#echo ">>>>> Looking for the biggest SMT2 trace among:\n $IN_FILES ..."

BIGGEST_TRACE=

for i in $IN_FILES; do
    curr_size=$(size $i)
    if [ $curr_size -gt $(size $BIGGEST_TRACE) ]
    then
        echo ">>>>> Found a bigger trace: $i (size==$curr_size)"
        BIGGEST_TRACE=$i
    fi;
done

#echo ">>>>> The size of the biggest trace is: $(size $BIGGEST_TRACE)"

Z3="$Z3_EXE TRACE=true PROOF=true $BIGGEST_TRACE"

echo ">>>>> Running $Z3"

eval $Z3

echo ">>>>> Done. You should find z3.log in your current directory."
