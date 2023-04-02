﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ColoredSquares;
using KModkit;
using System.Linq;

public class BicoloredSquaresModule : ColoredSquaresModuleBase {

	public KMBombInfo bombInfo;
	public override string Name { get { return "Bicolored Squares"; } }

	Dictionary<char, string> morseCharacterReferences = new Dictionary<char, string> {
        { 'A', ".-" }, { 'B', "-..." }, { 'C', "-.-." }, { 'D', "-.." }, { 'E', "." }, { 'F', "..-." },
		{ 'G', "--." }, { 'H', "...." }, { 'I', ".." }, { 'J', ".---" }, { 'K', "-.-" }, { 'L', ".-.." },
		{ 'M', "--" }, { 'N', "-." }, { 'O', "---" }, { 'P', ".--." }, { 'Q', "--.-" }, { 'R', ".-." },
		{ 'S', "..." }, { 'T', "-" }, { 'U', "..-" }, { 'V', "...-" }, { 'W', ".--" }, { 'X', "-..-" },
		{ 'Y', "-.--" }, { 'Z', "--.." }, { '0', "-----" }, { '1', ".----" }, { '2', "..---" }, { '3', "...--" },
		{ '4', "....-" }, { '5', "....." }, { '6', "-...." }, { '7', "--..." }, { '8', "---.." }, { '9', "----." },
	};

	static Dictionary<string, List<IEnumerable<int>>> globalHandlerPossibleLengths = new Dictionary<string, List<IEnumerable<int>>>();

	IEnumerable<char> selectedSerialNoCombination;
	IEnumerable<char> validSerialNo;

	List<IEnumerable<int>> possibleLengths = new List<IEnumerable<int>>();
	SquareColor? dotColor, dashColor;
	IEnumerable<SquareColor> lastRememberedBoard;
	bool solvePhaseActivated, forgivedInputMistake = false;
	string inputString;
	// Use this for initialization
	void Start() {
		validSerialNo = bombInfo.GetSerialNumber().Where(a => morseCharacterReferences.ContainsKey(a));
		var distinctLengths = validSerialNo.Select(a => morseCharacterReferences[a].Length).Distinct().OrderByDescending(b => b);
		LogDebug("Distinct lengths: {0}", distinctLengths.Join());
		if (!globalHandlerPossibleLengths.ContainsKey(distinctLengths.Join("")))
		{
			var curPossibleLengths = new List<IEnumerable<int>>();
			curPossibleLengths.Add(new int[0]);
			var iterationCount = 0;
			while (curPossibleLengths.Any() && iterationCount < 16)
			{
				var storedPossibleLengths = new List<IEnumerable<int>>();
				for (var x = 0; x < curPossibleLengths.Count; x++)
				{
					var curArray = curPossibleLengths[x];
					var remainingDistinctValues = distinctLengths.Where(a => curArray.Sum() + a <= 16 && (!curArray.Any() || curArray.Last() >= a)).OrderByDescending(a => a);
					for (var y = 0; y < remainingDistinctValues.Count(); y++)
					{
						var concatenatedList = curArray.Concat(new[] { remainingDistinctValues.ElementAt(y) });
						var uniqueList = true;
						for (var idx = 0; idx < storedPossibleLengths.Concat(possibleLengths).Count(); idx++)
						{
							var curListToCheck = storedPossibleLengths.Concat(possibleLengths).ElementAt(idx);
							var containsUnequalNums = false;
							foreach (int curLength in concatenatedList.Distinct())
							{
								containsUnequalNums |= concatenatedList.Count(a => a == curLength) != curListToCheck.Count(a => a == curLength);
							}
							uniqueList &= containsUnequalNums;
						}
						if (uniqueList)
							storedPossibleLengths.Add(concatenatedList);
					}
				}
				possibleLengths.AddRange(curPossibleLengths);
				curPossibleLengths.Clear();
				curPossibleLengths.AddRange(storedPossibleLengths);
				iterationCount++;
			}
			globalHandlerPossibleLengths.Add(distinctLengths.Join(""), possibleLengths);
		}
		else
        {
			possibleLengths = globalHandlerPossibleLengths[distinctLengths.Join("")];

		}
		LogDebug("All possible combinations: {0}", possibleLengths.Any() ? possibleLengths.Select(a => "[" + a.Join() + "]").Join(",") : "empty");
		PrepInitialPhase();
	}
	void PrepInitialPhase()
    {
		solvePhaseActivated = false;
		var possibleColors = new List<SquareColor> { SquareColor.Red, SquareColor.Green, SquareColor.Yellow, SquareColor.Blue, SquareColor.Magenta };
		possibleColors.Remove(possibleColors.PickRandom());
		for (var x = 0; x < _colors.Length; x++)
        {
			_colors[x] = possibleColors[x % possibleColors.Count];
        }
		_colors.Shuffle();
		StartSquareColorsCoroutine(_colors, SquaresToRecolor.All, true);
		dotColor = null;
		dashColor = null;
		Log("Four colors that are present on this module: {0}", possibleColors.Select(a => a.ToString()).Join());
		Log("Press two different colors to start disarming the module.");
		var filteredPossibleLengths = possibleLengths.Where(a => a.Sum() >= possibleLengths.Select(b => b.Sum()).Max());
		LogDebug("Filtered possible combinations (max length = {1}): {0}", filteredPossibleLengths.Any() ? filteredPossibleLengths.Select(a => "[" + a.Join() + "]").Join(",") : "empty", possibleLengths.Select(a => a.Sum()).Max());

		IEnumerable<IEnumerable<int>> nonoverlappablePossibleLengths = new List<IEnumerable<int>>();

		var serialNoLengths = validSerialNo.Distinct().Select(a => morseCharacterReferences[a].Length);
		var requiredDupeCounts = 0;
		while (!nonoverlappablePossibleLengths.Any() && requiredDupeCounts < 5)
		{
			nonoverlappablePossibleLengths = filteredPossibleLengths
			.Where(a => a.Distinct()
			.All(b => a.Count(c => c == b) - serialNoLengths.Count(c => c == b) <= requiredDupeCounts));
			if (!nonoverlappablePossibleLengths.Any())
				requiredDupeCounts++;
		}
		LogDebug("Filtered selected combinations by serial number characters' Morse representation + tolerate {1} repeated character(s): {0}", nonoverlappablePossibleLengths.Any() ? nonoverlappablePossibleLengths.Select(a => "[" + a.Join() + "]").Join(",") : "empty", requiredDupeCounts);
		var selectednonoverlappablePossibleLengths = nonoverlappablePossibleLengths.PickRandom();
		var selectedChars = new List<char>();
		foreach (int oneLength in selectednonoverlappablePossibleLengths.Distinct())
		{
			var curSerialNoChars = validSerialNo.Where(a => morseCharacterReferences[a].Length == oneLength).ToArray();
			while (selectednonoverlappablePossibleLengths.Count(a => a == oneLength) > selectedChars.Count(a => morseCharacterReferences[a].Length == oneLength))
			{
				var curDifference = selectednonoverlappablePossibleLengths.Count(a => a == oneLength) - selectedChars.Count(a => morseCharacterReferences[a].Length == oneLength);
				selectedChars.AddRange(curSerialNoChars.Shuffle().Take(curDifference));
			}
		}
		selectedSerialNoCombination = selectedChars;
	
		Log("The selected serial number combination selected [ {0} ] will be used in the solving phase.", selectedSerialNoCombination.Join(", "));
	}
	void ActivateSolvePhase()
	{
		SetAllButtonsBlack();
		var orderSelected = selectedSerialNoCombination.ToArray().Shuffle();
		var dashDotReferences = orderSelected.Select(a => morseCharacterReferences[a]).Join("");

		Log("The order of the characters [ {0} ] has been assigned.", orderSelected.Join(", "));
		
		for (var x = 0; x < _colors.Length; x++)
        {
			if (x < dashDotReferences.Length)
				_colors[x] = (SquareColor)(dashDotReferences.ElementAt(x) == '.' ^ x % 2 == 1 ? dotColor : dashColor );
			else
				_colors[x] = SquareColor.Black;
        }
		_colors.Shuffle();
		StartSquareColorsCoroutine(_colors, SquaresToRecolor.All, true);
		Log("{0} {1} square(s) and {2} {3} square(s) are present.", _colors.Count(a => a == dotColor), dotColor, _colors.Count(a => a == dashColor), dashColor);
		Log("One of expected Morse inputs to transmit (spaces being to press the white tile after each input): \"{0}\"", orderSelected.Select(a => morseCharacterReferences[a]).Join(" "));
		solvePhaseActivated = true;
	}


	protected override void ButtonPressed(int index)
	{
		
		if (!solvePhaseActivated)
		{
			var colorInInd = _colors[index];
			if (dotColor != null)
			{
				if (colorInInd == SquareColor.Black)
				{
					Log("You pressed the same square twice. The dot color and the dash color cannot both be {0}. Restarting...", dotColor.ToString());
					Strike();
					PrepInitialPhase();
				}
				else if (colorInInd == dotColor)
				{
					Log("You pressed two squares of the same color. The dot color and the dash color cannot both be {0}. Restarting...", dotColor.ToString());
					Strike();
					PrepInitialPhase();
				}
				else
				{
					PlaySound(index);
					dashColor = colorInInd;
					Log("You pressed 2 squares of the different color. The dot color will be {0} and the dash color will be {1}", dotColor.ToString(), dashColor.ToString());
					ActivateSolvePhase();
				}
			}
			else
			{
				PlaySound(index);
				dotColor = colorInInd;
				//_colors[index] = SquareColor.White;
				SetButtonColor(index, SquareColor.White);
			}
		}
		else
        {
			if (inputString == null)
			{
				inputString = "";
				lastRememberedBoard = _colors.ToArray();
			}
			if (_colors[index] == SquareColor.White)
            {
				PlaySound(index);
				if (validSerialNo.Any(a => morseCharacterReferences[a] == inputString))
				{
					for (var x = 0; x < _colors.Length; x++)
					{
						if (_colors[x] != SquareColor.White)
							SetButtonBlack(x);
						_colors[x] = _colors[x] == SquareColor.White ? SquareColor.Black : _colors[x];
					}
					Log("Inputted Morse string {0} corresponds to {1}, a valid character in the serial number.", inputString, validSerialNo.Single(a => morseCharacterReferences[a] == inputString));
					if (_colors.All(a => a == SquareColor.Black))
                    {
						Log("You cleared the entire grid.", inputString);
						ModulePassed();
						return;
                    }
					StartSquareColorsCoroutine(_colors, SquaresToRecolor.NonwhiteOnly, true);
					inputString = "";
					return;
				}

				Log("Inputted Morse string {0} does not correctly correspond to a valid serial number character.", inputString);
				if (forgivedInputMistake)
                {
					Log("You already have been forgiven once since the last reset. I can't forgive you again.");
					Strike();
					solvePhaseActivated = false;
					forgivedInputMistake = false;
					PrepInitialPhase();
					return;
                }
				Log("Forgiven. Your board has been reset back to the last state from the last correct series of inputs.");
				forgivedInputMistake = true;
				Audio.PlaySoundAtTransform("colorreset", transform);
				_colors = lastRememberedBoard.ToArray();
				StartSquareColorsCoroutine(_colors);
				inputString = "";
            }
			else if (_colors[index] != SquareColor.Black)
			{
				PlaySound(index);
				inputString += _colors[index] == dotColor ? "." : _colors[index] == dashColor ? "-" : "";
				_colors[index] = SquareColor.White;
				for (var x = 0; x < _colors.Length; x++)
                {
					_colors[x] = _colors[x] == dotColor ? (SquareColor)dashColor :
						_colors[x] == dashColor ? (SquareColor)dotColor : _colors[x];
				}
				
				StartSquareColorsCoroutine(_colors,delay: false);
			}
		}
	}

	IEnumerator TwitchHandleForcedSolve()
    {
		if (!solvePhaseActivated)
        {
			var selectedColors = _colors
				.Where(a => new[] { SquareColor.Blue, SquareColor.Magenta, SquareColor.Yellow, SquareColor.Red, SquareColor.Green }.Contains(a))
				.Distinct().ToArray().Shuffle().Take(2);

			var firstColorIdxes = Enumerable.Range(0, 16).Where(a => _colors[a] == selectedColors.First());
			var secondColorIdxes = Enumerable.Range(0, 16).Where(a => _colors[a] == selectedColors.Last());
			if (dotColor == null)
            {
				Buttons[firstColorIdxes.PickRandom()].OnInteract();
				yield return new WaitForSeconds(0.1f);
            }
			Buttons[selectedColors.Last() == dotColor ? firstColorIdxes.PickRandom() : secondColorIdxes.PickRandom()].OnInteract();
			yield return new WaitForSeconds(0.1f);
        }
		if (!_isSolved)
        {
			while (IsCoroutineActive)
				yield return true;


        }

    }
}