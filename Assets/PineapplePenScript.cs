using KModkit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class PineapplePenScript : MonoBehaviour {

    public KMAudio Audio;
    public KMBombInfo BombInfo;
    public KMBombModule Module;
    public KMSelectable[] buttons;
    public TextMesh pairDisp;

    private readonly int[][] _grid = new int[6][];
    private int _xPos;
    private int _yPos;
    private bool _penPickedUp;
    private bool _readyToSolve;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    internal bool _moduleSolved;

    private int _pineapplePenId;
    private static int _pineapplePenIdCounter = 1;

    internal ApplePenScript _partner;

    public TextMesh IdText;

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        _pineapplePenId = _pineapplePenIdCounter++;
        StartCoroutine(Init());
        Module.OnActivate += delegate ()
        {
            if (_pineapplePenId == 1 && !BombInfo.GetModuleNames().Contains("Apple Pen"))
                Audio.PlaySoundAtTransform("startup", transform);
        };
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].OnInteract +=  PressButton(i); 
            buttons[i].OnHighlight +=  HighlightButton(i); 
        }
        _penPickedUp = false;
        regen:
        int appleCt = UnityEngine.Random.Range(13, 17);
        for (int i = 0; i < 6; i++)
            _grid[i] = new int[6];
        List<string> usedSpots = new List<string>();
        for (int i = 0; i < appleCt + 2; i++)
        {
            redo:
            int pos1 = UnityEngine.Random.Range(0, 6);
            int pos2 = UnityEngine.Random.Range(0, 6);
            string spot = pos1.ToString() + pos2.ToString();
            if (usedSpots.Contains(spot))
                goto redo;
            usedSpots.Add(spot);
            if (i == appleCt)
                _grid[pos1][pos2] = 2;
            else if (i == appleCt + 1)
                _grid[pos1][pos2] = 3;
            else
                _grid[pos1][pos2] = 1;
        }
        redo2:
        _xPos = UnityEngine.Random.Range(0, 6);
        _yPos = UnityEngine.Random.Range(0, 6);
        string spot2 = _xPos.ToString() + _yPos.ToString();
        if (usedSpots.Contains(spot2))
            goto redo2;
        for (int k = 0; k < 2; k++)
        {
            var q = new Queue<int[]>();
            var allMoves = new List<Movement>();
            var startPoint = new int[] { _xPos, _yPos };
            var target = new int[] { -1, -1 };
            var doned = false;
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    if ((_grid[i][j] == 2 && k == 0) || (_grid[i][j] == 3 && k == 1))
                    {
                        target[0] = i;
                        target[1] = j;
                        goto skip;
                    }
                }
            }
            skip:
            q.Enqueue(startPoint);
            while (q.Count > 0)
            {
                var next = q.Dequeue();
                if (next[0] == target[0] && next[1] == target[1])
                {
                    doned = true;
                    break;
                }
                var allDirections = "UDLR";
                string paths = "";
                for (int b = 0; b < 4; b++)
                {
                    int[] newPos = GetNewPosition(next[0], next[1], b);
                    if (_grid[newPos[0]][newPos[1]] != 1)
                        paths += allDirections[b];
                }
                for (int i = 0; i < 4; i++)
                {
                    var check = GetNewPosition(next[0], next[1], i);
                    if (paths.Contains(allDirections[i]) && !allMoves.Any(x => x.start[0] == check[0] && x.start[1] == check[1]))
                    {
                        q.Enqueue(check);
                        allMoves.Add(new Movement { start = next, end = check, direction = i });
                    }
                }
            }
            if (!doned)
                goto regen;
        }
        Debug.LogFormat("[Pineapple Pen #{0}] Generated Grid:", _moduleId);
        for (int i = 0; i < 6; i++)
            Debug.LogFormat("[Pineapple Pen #{0}] {1}{2}{3}{4}{5}{6}", _moduleId, _grid[i][0], _grid[i][1], _grid[i][2], _grid[i][3], _grid[i][4], _grid[i][5]);
        Debug.LogFormat("[Pineapple Pen #{0}] Key: 0 = Nothing | 1 = Apple | 2 = Pen | 3 = Pineapple", _moduleId);
        Debug.LogFormat("[Pineapple Pen #{0}] Starting Position: {1}, {2} (row then column, 0-indexed from top left)", _moduleId, _xPos, _yPos);
    }

    private IEnumerator Init()
    {
        yield return null;
        var sn = BombInfo.GetSerialNumber();
        if (PPAPBehavior.Modules.ContainsKey(sn) && PPAPBehavior.Modules[sn].Count > 0)
        {
            _partner = PPAPBehavior.Modules[sn][0];
            _partner._partner = this;
            _partner.IdText.text = IdText.text = PPAPBehavior.Modules[sn].Count.ToString();
            PPAPBehavior.Modules[sn].RemoveAt(0);
        }
    }

    private KMSelectable.OnInteractHandler PressButton(int index)
    {
        return delegate ()
        {
            if (!_moduleSolved)
            {
                if (index == 4)
                {
                    SubmitPress();
                }
                else if (!_readyToSolve)
                {
                    buttons[index].AddInteractionPunch();
                    Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, buttons[index].transform);
                    int[] getDir = GetNewPosition(_xPos, _yPos, index);
                    Debug.LogFormat("[Pineapple Pen #{0}] Moved to: {1}, {2}", _moduleId, getDir[0], getDir[1]);
                    if (_grid[getDir[0]][getDir[1]] == 1)
                    {
                        Debug.LogFormat("[Pineapple Pen #{0}] You touched an apple, strike! Resetting...", _moduleId);
                        Module.HandleStrike();
                        Start();
                        return false;
                    }
                    else if (_grid[getDir[0]][getDir[1]] == 2 && !_penPickedUp)
                    {
                        _penPickedUp = true;
                        Debug.LogFormat("[Pineapple Pen #{0}] You picked up the pen.", _moduleId);
                    }
                    else if (_grid[getDir[0]][getDir[1]] == 3 && _penPickedUp)
                    {
                        _readyToSolve = true;
                        Audio.PlaySoundAtTransform("uh", transform);
                        Debug.LogFormat("[Pineapple Pen #{0}] You have made a pineapple pen, now press solve.", _moduleId);
                    }
                    _xPos = getDir[0];
                    _yPos = getDir[1];
                }
            }
            return false;
        };
    }

    private Action HighlightButton(int pressed)
    {
        return delegate ()
        {
            if (_readyToSolve != true)
            {
                if (pressed == 4)
                    return;
                int[] checkDir = GetNewPosition(_xPos, _yPos, pressed);
                if (_grid[checkDir[0]][checkDir[1]] == 1)
                    Audio.PlaySoundAtTransform("apple", transform);
                else if (_grid[checkDir[0]][checkDir[1]] == 2 && !_penPickedUp)
                    Audio.PlaySoundAtTransform("pen", transform);
                else if (_grid[checkDir[0]][checkDir[1]] == 3)
                    Audio.PlaySoundAtTransform("pineapple", transform);
            }
            return;
        };
    }

    int[] GetNewPosition(int oldX, int oldY, int dir)
    {
        int[] newPos = new int[2];
        switch (dir)
        {
            case 0:
                newPos[0] = oldX - 1;
                newPos[1] = oldY;
                if (newPos[0] < 0)
                    newPos[0] = 5;
                break;
            case 1:
                newPos[0] = oldX + 1;
                newPos[1] = oldY;
                if (newPos[0] > 5)
                    newPos[0] = 0;
                break;
            case 2:
                newPos[0] = oldX;
                newPos[1] = oldY - 1;
                if (newPos[1] < 0)
                    newPos[1] = 5;
                break;
            default:
                newPos[0] = oldX;
                newPos[1] = oldY + 1;
                if (newPos[1] > 5)
                    newPos[1] = 0;
                break;
        }
        return newPos;
    }

    //twitch plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} highlight <up/left/down/right> [Highlights the specified arrow button] | !{0} press <up/left/down/right/solve> [Presses the specified button] | Highlights and presses can be chained using spaces | You can simplify your inputs to their first letter";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        string[] parameters = command.Split(' ');
        if (Regex.IsMatch(parameters[0], @"^\s*highlight\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (parameters.Length == 1)
                yield return "sendtochaterror Please specify at least 1 button to highlight!";
            else
            {
                for (int i = 1; i < parameters.Length; i++)
                {
                    if (!parameters[i].ToLowerInvariant().EqualsAny("up", "down", "left", "right", "u", "d", "l", "r"))
                    {
                        yield return "sendtochaterror!f The specified arrow button '" + parameters[i] + "' is invalid!";
                        yield break;
                    }
                }
                yield return null;
                for (int i = 1; i < parameters.Length; i++)
                {
                    switch (parameters[i].ToLowerInvariant())
                    {
                        case "up":
                        case "u":
                            buttons[0].OnHighlight();
                            break;
                        case "down":
                        case "d":
                            buttons[1].OnHighlight();
                            break;
                        case "left":
                        case "l":
                            buttons[2].OnHighlight();
                            break;
                        default:
                            buttons[3].OnHighlight();
                            break;
                    }
                    yield return new WaitForSeconds(.75f);
                }
            }
            yield break;
        }
        if (Regex.IsMatch(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (parameters.Length == 1)
                yield return "sendtochaterror Please specify at least 1 button to press!";
            else
            {
                for (int i = 1; i < parameters.Length; i++)
                {
                    if (!parameters[i].ToLowerInvariant().EqualsAny("up", "down", "left", "right", "solve", "u", "d", "l", "r", "s"))
                    {
                        yield return "sendtochaterror!f The specified button '" + parameters[i] + "' is invalid!";
                        yield break;
                    }
                }
                yield return null;
                for (int i = 1; i < parameters.Length; i++)
                {
                    switch (parameters[i].ToLowerInvariant())
                    {
                        case "up":
                        case "u":
                            buttons[0].OnHighlight();
                            break;
                        case "down":
                        case "d":
                            buttons[1].OnHighlight();
                            break;
                        case "left":
                        case "l":
                            buttons[2].OnHighlight();
                            break;
                        case "right":
                        case "r":
                            buttons[3].OnHighlight();
                            break;
                        default:
                            buttons[4].OnHighlight();
                            break;
                    }
                    yield return new WaitForSeconds(.1f);
                }
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        if (_partner != null)
        {
            _partner.IdText.text = "-";
            _partner._partner = null;
        }
        _partner = null;
        IdText.text = "-";
        for (int k = 0; k < 2; k++)
        {
            if ((k == 0 && _penPickedUp) || (k == 1 && _readyToSolve))
                continue;
            var q = new Queue<int[]>();
            var allMoves = new List<Movement>();
            var startPoint = new int[] { _xPos, _yPos };
            var target = new int[] { -1, -1 };
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    if ((_grid[i][j] == 2 && k == 0) || (_grid[i][j] == 3 && k == 1))
                    {
                        target[0] = i;
                        target[1] = j;
                        goto skip;
                    }
                }
            }
            skip:
            q.Enqueue(startPoint);
            while (q.Count > 0)
            {
                var next = q.Dequeue();
                if (next[0] == target[0] && next[1] == target[1])
                    goto readyToSubmit;
                var allDirections = "UDLR";
                string paths = "";
                for (int b = 0; b < 4; b++)
                {
                    int[] newPos = GetNewPosition(next[0], next[1], b);
                    if (_grid[newPos[0]][newPos[1]] != 1)
                        paths += allDirections[b];
                }
                for (int i = 0; i < 4; i++)
                {
                    var check = GetNewPosition(next[0], next[1], i);
                    if (paths.Contains(allDirections[i]) && !allMoves.Any(x => x.start[0] == check[0] && x.start[1] == check[1]))
                    {
                        q.Enqueue(check);
                        allMoves.Add(new Movement { start = next, end = check, direction = i });
                    }
                }
            }
            throw new InvalidOperationException("There is a bug in Pineapple Pen's autosolver.");
            readyToSubmit:
            var target2 = new int[] { target[0], target[1] };
            var lastMove = allMoves.First(x => x.end[0] == target2[0] && x.end[1] == target2[1]);
            var relevantMoves = new List<Movement> { lastMove };
            while (lastMove.start != startPoint)
            {
                lastMove = allMoves.First(x => x.end[0] == lastMove.start[0] && x.end[1] == lastMove.start[1]);
                relevantMoves.Add(lastMove);
            }
            for (int i = 0; i < relevantMoves.Count; i++)
            {
                buttons[relevantMoves[relevantMoves.Count - 1 - i].direction].OnInteract();
                yield return new WaitForSeconds(.1f);
            }
        }
        buttons[4].OnInteract();
    }

    class Movement
    {
        public int[] start;
        public int[] end;
        public int direction;
    }

    internal void SubmitPress(bool fromPartner = false)
    {
        buttons[4].AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, buttons[4].transform);
        if (!_readyToSolve)
        {
            Debug.LogFormat("[Pineapple Pen #{0}] You have not made a pineapple pen yet, strike! Resetting...", _moduleId);
            Module.HandleStrike();
            Start();
        }
        else
        {
            _moduleSolved = true;
            Debug.LogFormat("[Pineapple Pen #{0}] Module solved.", _moduleId);
            Module.HandlePass();
            StartCoroutine(CheckIfBothSolved());
        }
        if (_partner != null && !fromPartner)
            _partner.SubmitPress(true);
    }

    private IEnumerator CheckIfBothSolved()
    {
        yield return null;
        if (_partner != null)
        {
            if (!_partner._moduleSolved)
            {
                Audio.PlaySoundAtTransform("pineapple", transform);
                _partner.IdText.text = "-";
                _partner._partner = null;
                IdText.text = "-";
                _partner = null;
                yield return new WaitForSeconds(0.675f);
                Audio.PlaySoundAtTransform("pen", transform);
            }
        }
        else
        {
            Audio.PlaySoundAtTransform("pineapple", transform);
            yield return new WaitForSeconds(0.675f);
            Audio.PlaySoundAtTransform("pen", transform);
        }
    }
}