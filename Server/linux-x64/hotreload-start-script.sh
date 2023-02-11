#!/bin/sh

set -e

TMPPATH=""
CLIARGUMENTS_FILE=""
EXECUTABLESOURCEDIR=""
TITLE=""
METHODPATCHDIR=""
PIDFILE=""

while [ "$1" != "" ]; do
    case $1 in
        -t | --tmp-path )
            shift
            TMPPATH="$1"
            ;;
        -c | --cli-arguments-file )
            shift
            CLIARGUMENTS_FILE="$1"
            ;;
        -d | --executables-source-dir )
            shift
            EXECUTABLESOURCEDIR="$1"
            ;;
        -t | --title )
            shift
            TITLE="$1"
            ;;
        -p | --pidfile )
            shift
            PIDFILE="$1"
            ;;
        -m | --method-patch-dir )
            shift
            METHODPATCHDIR="$1"
            ;;
    esac
    shift
done

if [ -z "$TMPPATH" ] || [ -z "$CLIARGUMENTS_FILE" ] || [ -z "$EXECUTABLESOURCEDIR" ] || [ -z "$TITLE" ] || [ -z "$PIDFILE" ] || [ -z "$METHODPATCHDIR" ]; then
    echo "Missing arguments"
    exit 1
fi

CLIARGUMENTS="$(cat $CLIARGUMENTS_FILE)"
rm "$CLIARGUMENTS_FILE"

# Needs be removed if you have multiple unities
pgrep CodePatcherCLI | xargs -I {} kill {}

rm -rf "$TMPPATH"
mkdir -p "$METHODPATCHDIR"
mkdir -p "$TMPPATH/executables"

cp -f "$EXECUTABLESOURCEDIR/CodePatcherCLI" "$TMPPATH/executables/CodePatcherCLI"

TERMINALRUNSCRIPT="$EXECUTABLESOURCEDIR/terminal-run.sh"
sed -i 's/\r//g' "$TERMINALRUNSCRIPT"

chmod +x "$TERMINALRUNSCRIPT"
chmod +x "$TMPPATH/executables/CodePatcherCLI"

HAVETERMINAL=""
"$TERMINALRUNSCRIPT" && HAVETERMINAL="yes"

INTERNALSCRIPT="$TMPPATH/executables/hotreload-internal-start"


# see doc/linux-system-freeze.org why I put the nice

if [ -n "$HAVETERMINAL" ]; then
    cat << EOF > $INTERNALSCRIPT
#!/bin/sh
echo \$\$ > "$PIDFILE"
nice -n 5 "$TMPPATH/executables/CodePatcherCLI" $CLIARGUMENTS || read
EOF

    chmod +x "$INTERNALSCRIPT"
    "$TERMINALRUNSCRIPT" "$TITLE" "$INTERNALSCRIPT"
else
    printf "Don't have a terminal to run, printing to unity console instead. Consider hacking:\n$TERMINALRUNSCRIPT\n"
    echo $$ > "$PIDFILE"
    exec "$TMPPATH/executables/CodePatcherCLI" $CLIARGUMENTS
fi
