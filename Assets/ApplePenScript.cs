using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;

public class ApplePenScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public KMSelectable[] ArrowSels;
    public KMSelectable[] DisplaySels;
    public KMSelectable SubmitSel;

    public Material[] ShapeMats;
    public GameObject ScreenObj;
    public TextMesh[] ArrowTexts;
    public GameObject LedObj;
    public Material[] LedMats;

    public TextMesh IdText;

    public enum Item
    {
        Apple,
        Pen,
        Pineapple
    }

    public class ApplePenPair : IEquatable<ApplePenPair>
    {
        public Item A;
        public Item B;
        public ApplePenPair(Item a, Item b)
        {
            A = a;
            B = b;
        }
        public bool Equals(ApplePenPair other)
        {
            return other != null && other.A == A && other.B == B;
        }
    }
    private static readonly ApplePenPair _base = new ApplePenPair(Item.Apple, Item.Pen);
    private int[] _solution = new int[2];

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    internal bool _moduleSolved;

    private readonly Item[] _grid = new Item[36];
    private int _startPos;
    private int _currentPos;
    private bool _startupSound;
    private int[] _currentInput = new int[2];
    private Coroutine _flashCoroutine;

    private int _applePenId;
    private static int _applePenIdCounter = 1;

    internal PineapplePenScript _partner;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        _applePenId = _applePenIdCounter++;
        Module.OnActivate += delegate () { if (_applePenId == 1) Audio.PlaySoundAtTransform("startup", transform); };
        _currentInput = Enumerable.Range(1, 5).ToArray().Shuffle().Take(2).ToArray();
        _currentPos = Rnd.Range(0, 36);

        var sn = BombInfo.GetSerialNumber();
        if (!PPAPBehavior.Modules.ContainsKey(sn))
            PPAPBehavior.Modules[sn] = new List<ApplePenScript>();
        PPAPBehavior.Modules[sn].Add(this);

        for (int btn = 0; btn < 4; btn++)
            ArrowSels[btn].OnInteract += MovePress(btn);
        for (int btn = 0; btn < 4; btn++)
            DisplaySels[btn].OnInteract += DisplayPress(btn);
        SubmitSel.OnInteract += SubmitPress();

        ArrowTexts[0].text = _currentInput[0].ToString();
        ArrowTexts[1].text = _currentInput[1].ToString();
        Generate();
        UpdateVisuals();
    }

    private KMSelectable.OnInteractHandler MovePress(int btn)
    {
        return delegate ()
        {
            ArrowSels[btn].AddInteractionPunch(0.5f);
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, ArrowSels[btn].transform);
            if (_moduleSolved)
                return false;
            int x = _currentPos % 6;
            int y = _currentPos / 6;
            if (btn == 0)
                y = (y + 5) % 6;
            if (btn == 1)
                x = (x + 1) % 6;
            if (btn == 2)
                y = (y + 1) % 6;
            if (btn == 3)
                x = (x + 5) % 6;
            _currentPos = y * 6 + x;
            UpdateVisuals();
            return false;
        };
    }

    private void OnDestroy()
    {
        _applePenIdCounter = 1;
    }

    private KMSelectable.OnInteractHandler DisplayPress(int btn)
    {
        return delegate ()
        {
            if (_moduleSolved)
                return false;
            DisplaySels[btn].AddInteractionPunch(0.5f);
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, DisplaySels[btn].transform);
            if (btn == 0)
                _currentInput[0] = ((_currentInput[0] + 5) % 5) + 1;
            if (btn == 1)
                _currentInput[0] = ((_currentInput[0] + 3) % 5) + 1;
            if (btn == 2)
                _currentInput[1] = ((_currentInput[1] + 5) % 5) + 1;
            if (btn == 3)
                _currentInput[1] = ((_currentInput[1] + 3) % 5) + 1;
            ArrowTexts[0].text = _currentInput[0].ToString();
            ArrowTexts[1].text = _currentInput[1].ToString();
            return false;
        };
    }

    internal KMSelectable.OnInteractHandler SubmitPress(bool fromPartner = false)
    {
        return delegate ()
        {
            if (_moduleSolved)
                return false;
            SubmitMethod();
            return false;
        };
    }

    internal void SubmitMethod(bool fromPartner = false)
    {
        CheckAnswer();
        if (_partner != null && !fromPartner)
            _partner.SubmitMethod(true);
    }

    private void CheckAnswer()
    {
        int curX = _startPos % 6;
        int curY = _startPos / 6;
        var list = new List<ApplePenPair>();
        for (int iter = 0; iter < 4; iter++)
        {
            curX = (curX + _currentInput[0]) % 6;
            var posA = curY * 6 + curX;
            curY = (curY + _currentInput[1]) % 6;
            var posB = curY * 6 + curX;
            list.Add(new ApplePenPair(_grid[posA], _grid[posB]));
        }
        if (!list[0].Equals(_base) && !list[1].Equals(_base) && !list[2].Equals(_base) && list[3].Equals(_base))
        {
            _moduleSolved = true;
            Debug.LogFormat("[Apple Pen #{0}] Correctly submitted {1} {2}. Module solved.", _moduleId, _currentInput[0], _currentInput[1]);
            Module.HandlePass();
            StartCoroutine(CheckIfBothSolved());
        }
        else
        {
            Debug.LogFormat("[Apple Pen #{0}] Incorrectly submitted {1} {2}. Strike.", _moduleId, _currentInput[0], _currentInput[1]);
            Module.HandleStrike();
        }
    }

    private IEnumerator CheckIfBothSolved()
    {
        yield return null;
        if (_partner != null)
        {
            if (_partner._moduleSolved)
                Audio.PlaySoundAtTransform("doublesolve", transform);
            else
            {
                Audio.PlaySoundAtTransform("apple", transform);
                _partner.IdText.text = "-";
                _partner._partner = null;
                IdText.text = "-";
                _partner = null;
                yield return new WaitForSeconds(0.475f);
                Audio.PlaySoundAtTransform("pen", transform);
            }
        }
        else
        {
            Audio.PlaySoundAtTransform("apple", transform);
            yield return new WaitForSeconds(0.475f);
            Audio.PlaySoundAtTransform("pen", transform);
        }
    }

    private void UpdateVisuals()
    {
        if (_flashCoroutine != null)
            StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(FlashImage());
    }

    private IEnumerator FlashImage()
    {
        ScreenObj.SetActive(false);
        yield return new WaitForSeconds(0.05f);
        ScreenObj.GetComponent<MeshRenderer>().material = ShapeMats[(int)_grid[_currentPos]];
        ScreenObj.SetActive(true);
        if (_currentPos == _startPos)
            LedObj.GetComponent<MeshRenderer>().material = LedMats[1];
        else
            LedObj.GetComponent<MeshRenderer>().material = LedMats[0];
        if (_startupSound)
            Audio.PlaySoundAtTransform(_grid[_currentPos].ToString().ToLowerInvariant(), transform);
        _startupSound = true;
    }

    private void Generate()
    {
        tryAgain:
        var directions = Enumerable.Range(1, 5).ToArray().Shuffle().Take(2).ToArray();
        for (int i = 0; i < _grid.Length; i++)
            _grid[i] = (Item)Rnd.Range(0, 3);
        _startPos = Rnd.Range(0, 36);
        int curX = _startPos % 6;
        int curY = _startPos / 6;
        var list = new List<ApplePenPair>();
        for (int iter = 0; iter < 4; iter++)
        {
            curX = (curX + directions[0]) % 6;
            var posA = curY * 6 + curX;
            curY = (curY + directions[1]) % 6;
            var posB = curY * 6 + curX;
            list.Add(new ApplePenPair(_grid[posA], _grid[posB]));
        }
        if (!list[0].Equals(_base) && !list[1].Equals(_base) && !list[2].Equals(_base) && list[3].Equals(_base))
        {
            _solution[0] = directions[0];
            _solution[1] = directions[1];
            goto done;
        }
        goto tryAgain;
        done:
        Debug.LogFormat("[Apple Pen #{0}] Grid:", _moduleId);
        Debug.LogFormat("[Apple Pen #{0}] {1}", _moduleId, _grid.Select(i => i.ToString()).Select(j => j == "Pineapple" ? "N" : j == "Apple" ? "A" : "P").Join(" "));
        Debug.LogFormat("[Apple Pen #{0}] Key: A = Apple | P = Pen | N = Pineapple", _moduleId);
        Debug.LogFormat("[Apple Pen #{0}] Starting position: {1}", _moduleId, "ABCDEF"[_startPos % 6].ToString() + ((_startPos / 6) + 1).ToString());
        Debug.LogFormat("[Apple Pen #{0}] Possible solution: {1} right, {2} down.", _moduleId, _solution[0], _solution[1]);
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} move urdl [Move up, right, down, left.] | !{0} set 2 4 [set displays to 2 4.] | !{0} submit [Press the solve button.]";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim().ToLowerInvariant();
        if (command.StartsWith("move "))
        {
            var cmd = command.Substring(4);
            var list = new List<KMSelectable>();
            for (int i = 0; i < cmd.Length; i++)
            {
                var str = "urdl ";
                int ix = str.IndexOf(cmd[i]);
                if (ix == -1)
                    yield break;
                if (ix == 4)
                    continue;
                list.Add(ArrowSels[ix]);
            }
            if (list.Count == 0)
                yield break;
            yield return null;
            yield return list;
            yield break;
        }
        var m = Regex.Match(command, @"^\s*set\s+(\d)\s+(\d)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            var input = new int[2];
            if (!int.TryParse(m.Groups[1].Value, out input[0]) || !int.TryParse(m.Groups[2].Value, out input[1]) || input[0] < 1 || input[0] > 5 || input[1] < 1 || input[1] > 5)
                yield break;
            yield return null;
            for (int i = 0; i < 2; i++)
                while (_currentInput[i] != input[i])
                {
                    DisplaySels[i * 2].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
            yield break;
        }
        m = Regex.Match(command, @"\s*submit\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            SubmitSel.OnInteract();
            yield break;
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        if (_partner != null)
        {
            _partner.IdText.text = "-";
            _partner._partner = null;
        }
        IdText.text = "-";
        _partner = null;
        for (int i = 0; i < 2; i++)
            while (_currentInput[i] != _solution[i])
            {
                DisplaySels[i * 2].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
        SubmitSel.OnInteract();
    }
}
