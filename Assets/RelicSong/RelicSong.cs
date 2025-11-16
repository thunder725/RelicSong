using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public class RelicSong : MonoBehaviour {

    // Presentation & Visual Data
	readonly float[] ariaModeNoteLocationX = new float[] { -0.03f, -0.01f, 0.01f, 0.03f, 0.05f };
    readonly float[] pirouetteModeNoteLocationX = new float[] { -0.05f, -0.03f, -0.01f, 0.01f, 0.03f };
    readonly float[] noteHeightsY = new float[] { -0.025f, -0.0125f, 0.0f, 0.0125f, 0.025f };
    public float ariaModeClefLocationX, pirouetteModeClefLocationY;
    public KMSelectable clef;
    public GameObject[] wholeNotes;
    public KMSelectable[] pressableNotes;
    public Material ariaMaterial, pirouetteMaterial, shinyMaterial;
    public MeshRenderer musicSheetCubeRenderer;
    public TextMesh stageCounterRenderer;
    public MeshRenderer noteApparitionFxPlane;
    Material noteApparitionFxMaterial;
    Vector3 wholeNoteScale;
    int[] _pressableNoteHeight = new int[5] { 1, 2, 3, 4, 5 };
    public AudioClip[] noteSounds;
    public AudioClip solveSound;



    // Module Data
    public KMBombModule thisModule;
    public KMBossModule thisBossModule;
    public KMAudio thisModuleAudio;
    public KMBombInfo thisBombInfo;
    string[] ignoredModulesList;
    int totalStageCount;
    int currentNumberOfSolves, currentStageNumber;

    int numberOfNotesInCurrentStage;
    int numberOfEmptyNotesInCurrentStage;
    List<int> currentStageNotes, currentValidAriaNotes, currentValidPirouetteNotes;
    enum RelicSongMode { Aria, Pirouette};
    RelicSongMode currentMode;
    bool changedModeThisStage;
    Coroutine revealStageCoroutine;
    Coroutine checkForSolvesCoroutine;
    List<int> currentlySubmittedNotes;
    bool hasSubmittedAriaAlready;

    // Logging Data - Formatting & naming from Royal_Flu$h
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;
    bool isInSubmitMode;




    // Buttons gathering and GetComponents
    void Awake()
    {
        // Initialize Logging
        moduleId = moduleIdCounter++;


        // Create a new material instance, to avoid editing every Relic Song modules at once when one solves
        noteApparitionFxMaterial = Instantiate<Material>(noteApparitionFxPlane.material);
        noteApparitionFxPlane.material = noteApparitionFxMaterial;

        wholeNoteScale = wholeNotes[0].transform.localScale;


        // Add interaction reactions
        clef.OnInteract += delegate () { ClefGetsPressed(); return false; };

        // I would have prefered using a for loop to have an index and not use Array.IndexOf()
        // But doing pressableNotes[i] returned an OutOfIndex error
        // That is because the link to "i" stays, but the variable has been cleared (or ++'d into oblivion?) so it doesn't work
        // We need a variable-autonomous way of initializing, which is why Array.IndexOf() is needed
        foreach (var _pressableNote in pressableNotes)
        {
            _pressableNote.OnInteract += delegate () { EmptyNoteGetsPressed(Array.IndexOf(pressableNotes, _pressableNote), _pressableNoteHeight[Array.IndexOf(pressableNotes, _pressableNote)], _pressableNote); return false; };
        }
    }


    // Puzzle Initialization
    void Start() 
    {
        Debug.LogFormat("[Relic Song #{0}] Initializing Module.", moduleId);
        GetModuleListData();

        // Initialize empty Arrays
        currentStageNotes = new List<int> { };
        currentValidAriaNotes = new List<int> { };
        currentValidPirouetteNotes = new List<int> { };

        isInSubmitMode = false; 

        // Clear the visual Whole Notes
        foreach (GameObject _note in wholeNotes)
        {
            _note.SetActive(false);
        }
        // Clear the visual Empty Notes
        foreach (KMSelectable _note in pressableNotes)
        {
            _note.gameObject.SetActive(false);
        }
        // Hide Note Apparition Fx Plane
        noteApparitionFxPlane.gameObject.SetActive(false);

        // Start in Green Mode
        currentMode = RelicSongMode.Aria;

        checkForSolvesCoroutine = StartCoroutine(CheckForSolves());


        if (totalStageCount == 0)
        {
            Debug.LogFormat("[Relic Song #{0}] Woops! Looks like no other modules exist!", moduleId, totalStageCount);
            EnterSubmissionMode();
        }
    }


    void NewStageHappens()
    {
        if (revealStageCoroutine != null)
        {
            StopCoroutine(revealStageCoroutine);
        }
        
        currentStageNumber++;

        // Update visual Stage Number
        // Make sure the one-digit stages still have a leading 0 =>   01 instead of 1
        string newStageNumber = currentStageNumber.ToString();
        if (newStageNumber.Length == 1)
        {
            newStageNumber = "0" + newStageNumber;
        }
        stageCounterRenderer.text = newStageNumber;



        // Clear the visual Notes
        foreach (GameObject _note in wholeNotes)
        {
            _note.SetActive(false);
        }
        // This VFX should be invisible already, but just in case it is interrupted mid-stage we hide it
        noteApparitionFxPlane.gameObject.SetActive(false);


        // Special Case if we're at the last stage, enter Submit Mode!
        if (currentStageNumber == totalStageCount)
        {
            EnterSubmissionMode();
            return;
        }


        // Else, if we're in a regular Stage.
        Debug.LogFormat("[Relic Song #{0}] =-=-= Stage #{1} =-=-=", moduleId, currentStageNumber);

        // We should change Mode more often than not
        // But first stage starts Green always
        if (currentStageNumber == 1)
        {
            changedModeThisStage = false;
        }
        else
        {
            changedModeThisStage = UnityEngine.Random.value <= 0.9f;
        }
        

        if (changedModeThisStage)
        {
            // Switch Mode internally
            currentMode = currentMode == RelicSongMode.Aria ? RelicSongMode.Pirouette : RelicSongMode.Aria;

            // Switch Sheet Music material
            musicSheetCubeRenderer.material = currentMode == RelicSongMode.Aria ? ariaMaterial : pirouetteMaterial;

            // Move the Clef to the other side of the Sheet Music
            StartCoroutine(MoveClefToOtherSide());

            Debug.LogFormat("[Relic Song #{0}] Switching Mode. Current Mode = {1}.", moduleId, currentMode == RelicSongMode.Aria ? "Green" : "Orange");
        }
        else
        {
            Debug.LogFormat("[Relic Song #{0}] Not switching Mode. Current Mode = {1}.", moduleId, currentMode == RelicSongMode.Aria ? "Green" : "Orange");
        }


        // Generate next Stage
        GenerateNextStage();

        // Reveal the Next Stage Notes
        revealStageCoroutine = StartCoroutine(RevealNextStage());
    }


    void GenerateNextStage()
    {

        // Generate number of Notes for this stage [3-5]
        float _random = UnityEngine.Random.value;
        // We want uneven randomness, more chance to get 3 than 4 than 5
        if (_random <= 0.5f)
        {
            numberOfNotesInCurrentStage = 3;
        }
        else if (_random <= 0.8f)
        {
            numberOfNotesInCurrentStage = 4;
        }
        else
        {
            numberOfNotesInCurrentStage = 5;
        }

        // Also we might add additional "0" notes so that the notes aren't always at the very start and all stuck together
        // This is just for visual flair but adds some code.
        // Since the range is Inclusive-Exclusive, we can't do 5-Notes but need to do 6-Notes
        numberOfEmptyNotesInCurrentStage = UnityEngine.Random.Range(0, 6 - numberOfNotesInCurrentStage);


        // Clear the previous stage
        currentStageNotes.Clear();


        // We allow maximum one duplicate of each note
        List<int> _possibleNotes = new List<int>() { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5 };
        int _addedNoteIndex;

        for (int i = 0; i < numberOfNotesInCurrentStage; i++)
        {
            // Select the note added to the current Stage
            _addedNoteIndex = UnityEngine.Random.Range(0, _possibleNotes.Count);

            // Add it to the current Stage
            currentStageNotes.Add(_possibleNotes[_addedNoteIndex]);

            // Remove it from the possible notes
            _possibleNotes.RemoveAt(_addedNoteIndex);
        }

        // Add empty 0s
        if (numberOfEmptyNotesInCurrentStage != 0)
        {
            for (int i = 0; i < numberOfEmptyNotesInCurrentStage; i++)
            {
                currentStageNotes.Add(0);
            }

            // Shuffle if we added the 0s only because otherwise the order is already random
            currentStageNotes = currentStageNotes.Shuffle();
        }


        // Log it
        Debug.LogFormat("[Relic Song #{0}] Generated a total of {1} Notes: {2}.", moduleId, numberOfNotesInCurrentStage, BuildLogStringWithNotes(currentStageNotes));


        ComputeCurrentValidNotes();
    }


    string BuildLogStringWithNotes(List<int> notes, bool removeZeros = true)
    {
        // Build a Log string with all notes
        string _notesString = "";

        if (notes.Count == 0)
        {
            return _notesString;
        }

        int _noteCount = 0;

        if (removeZeros)
        {
            // Ignore the 0s because they are here for visual placement
            foreach (int _note in notes)
            {
                if (_note != 0)
                {
                    _notesString += _note.ToString() + ", ";
                    _noteCount++;
                }

            }
        }
        else
        {
            // Ignore the 0s because they are here for visual placement
            foreach (int _note in notes)
            {
                _notesString += _note.ToString() + ", ";
                _noteCount++;
            }
        }

        
        if (_notesString.Length != 0)
        {
            // Remove the last unused ", "
            _notesString = _notesString.Remove(_noteCount * 3 - 2);
        }


        return _notesString;
    }


    void ComputeCurrentValidNotes()
    {


        for (int i = 0; i < currentStageNotes.Count; i++)
        {
            if (currentStageNotes[i] == 0) { continue; }

            if (currentMode == RelicSongMode.Aria)
            {

                // Prioritize Cancelling other notes
                if (currentValidPirouetteNotes.Contains(currentStageNotes[i]))
                {
                    currentValidPirouetteNotes.Remove(currentStageNotes[i]);
                    continue;
                }

                // Then add if not duplicate
                if (!currentValidAriaNotes.Contains(currentStageNotes[i]))
                {
                    currentValidAriaNotes.Add(currentStageNotes[i]);
                }
            }
            else
            {

                // Prioritize Cancelling other notes
                if (currentValidAriaNotes.Contains(currentStageNotes[i]))
                {
                    currentValidAriaNotes.Remove(currentStageNotes[i]);
                    continue;
                }

                // Then add if not duplicate
                if (!currentValidPirouetteNotes.Contains(currentStageNotes[i]))
                {
                    currentValidPirouetteNotes.Add(currentStageNotes[i]);
                }
            }
        }



        currentValidPirouetteNotes.Sort();
        currentValidAriaNotes.Sort();


        Debug.LogFormat("[Relic Song #{0}] Current valid Notes are:", moduleId);
        Debug.LogFormat("[Relic Song #{0}] Green: {1}", moduleId, BuildLogStringWithNotes(currentValidAriaNotes));
        Debug.LogFormat("[Relic Song #{0}] Orange: {1}", moduleId, BuildLogStringWithNotes(currentValidPirouetteNotes));
    }


    IEnumerator MoveClefToOtherSide()
    {
        // Cache target Location
        float _XPosition = currentMode == RelicSongMode.Aria ? ariaModeClefLocationX : pirouetteModeClefLocationY;
        Vector3 _TargetLocation = new Vector3(_XPosition, clef.transform.localPosition.y, 0f);
        // Cache Starting Location
        Vector3 _StartingLocation = clef.transform.localPosition;

        float _currentLerpTimer = 0f;
        float _easedMovementLerp = 0f;
        Vector3 _clefAngle = new Vector3(0, currentMode == RelicSongMode.Aria ? 15f : -15f, 0);

        while (_currentLerpTimer < 1)
        {
            _currentLerpTimer += Time.deltaTime * .55f;

            // Use Easing because it looks prettier
            _easedMovementLerp = Easing.OutQuart(_currentLerpTimer, 0, 1, 1);

            // Move
            clef.transform.localPosition = Vector3.Lerp(_StartingLocation, _TargetLocation, _easedMovementLerp);
            clef.transform.localEulerAngles = Vector3.Lerp(_clefAngle, Vector3.zero, _easedMovementLerp);

            // Wait for next frame
            yield return null;
        }

    }


    IEnumerator RevealNextStage()
    {
        if (changedModeThisStage)
        {
            yield return new WaitForSeconds(0.75f);
        }
        else
        {
            yield return new WaitForSeconds(0.25f);
        }

        // Initialize -1 so we start at 0
        int _currentlyRevealedNotes = -1;
        int _currentCodeNoteIndex = -1;

        // Prepare temp cache variables
        float noteXLocation = 0;
        Vector3 noteLocation;

        // Setup variables for all the apparition timers
        float _currentLerpTimer;
        float _easedScaleLerp;
        float _easedOpacityLerp;
        float _easedNoteScaleLerp;


        // For each actual note to show
        while (_currentlyRevealedNotes < numberOfNotesInCurrentStage - 1)
        {
            // Actual note counter
            _currentlyRevealedNotes++;

            // Actual Note index in the Array isn't just the same as the note counter
            // Because there are 0s
            _currentCodeNoteIndex++;
            while (currentStageNotes[_currentCodeNoteIndex] == 0)
            {
                _currentCodeNoteIndex++;
            }


            // Prepare its reveal location
            noteXLocation = currentMode == RelicSongMode.Aria ? ariaModeNoteLocationX[_currentCodeNoteIndex] : pirouetteModeNoteLocationX[_currentCodeNoteIndex];
            noteLocation = new Vector3(noteXLocation, 0.02f, noteHeightsY[currentStageNotes[_currentCodeNoteIndex] - 1]);

            // Set location and show
            wholeNotes[_currentlyRevealedNotes].transform.localPosition = noteLocation;
            wholeNotes[_currentlyRevealedNotes].SetActive(true);

            // Show the FX Apparition Plane
            noteApparitionFxPlane.transform.localPosition = noteLocation;
            noteApparitionFxPlane.gameObject.SetActive(true);
            // Random rotation so that it's a bit better
            noteApparitionFxPlane.transform.localEulerAngles = new Vector3(90, 0, UnityEngine.Random.Range(0f, 360f));

            // Play SFX
            thisModuleAudio.PlaySoundAtTransform(noteSounds[currentStageNotes[_currentCodeNoteIndex] - 1].name, transform);

            // Reset the Timer
            _currentLerpTimer = 0f;


            // For this singular note, animate the FX and the Note popping up
            while (_currentLerpTimer <= 1f)
            {
                // Increase Timer
                _currentLerpTimer += Time.deltaTime * 1.5f;

                // Use Easing because it looks prettier
                _easedScaleLerp = Easing.OutQuad(_currentLerpTimer, 0f, 0.13f, 1f);
                _easedOpacityLerp = Easing.InQuad(_currentLerpTimer, 1f, 0f, 1f);

                // This is a special NoteScale so that it pops up a bit,
                // with a shorter duration so it's quicker than the whole thing
                _easedNoteScaleLerp = Easing.OutQuad(Mathf.Clamp(_currentLerpTimer, 0, 0.4f), 0.6f, 1f, 0.4f);


                // Scale Up & Rotate the FX
                noteApparitionFxPlane.transform.localScale = new Vector3(_easedScaleLerp, _easedScaleLerp, _easedScaleLerp);
                noteApparitionFxPlane.transform.Rotate(0, 0, -60f * Time.deltaTime);

                // Fade out the FX
                noteApparitionFxMaterial.SetFloat("_Opacity", _easedOpacityLerp);

                // Scale Up the Note
                wholeNotes[_currentlyRevealedNotes].transform.localScale = _easedNoteScaleLerp * wholeNoteScale;


                yield return null;
            }


            // Should be invisible, but still, disable the FX
            noteApparitionFxPlane.gameObject.SetActive(false);

            yield return new WaitForSeconds(0.2f);

        }
    }


    void EnterSubmissionMode()
    {
        isInSubmitMode = true;
        StopCoroutine(checkForSolvesCoroutine);


        // Force switch stage to Green
        changedModeThisStage = currentMode != RelicSongMode.Aria;
        currentMode = RelicSongMode.Aria;
        // Switch Sheet Music material
        musicSheetCubeRenderer.material = ariaMaterial;

        // Only do the animation if we actually needed to change mode!
        if (changedModeThisStage)
        {
            StartCoroutine(MoveClefToOtherSide());
        }


        Debug.LogFormat("[Relic Song #{0}] =-=-= All other non-ignored modules solved. Entering Submission Mode. Switching to Green Mode. =-=-=", moduleId);

        currentlySubmittedNotes = new List<int>();

        PlaceEmptyNotesOnSheetMusic();
        
    }

    void PlaceEmptyNotesOnSheetMusic()
    {
        // Initialize variables for randomly placing the Empty Notes 
        _pressableNoteHeight = _pressableNoteHeight.Shuffle();
        int[] _pressableNoteXIndex = new int[5] { 1, 2, 3, 4, 5 }.Shuffle();
        float _noteXLocation;
        Vector3 _noteLocation;

        // Show all Empty Notes
        for (int i = 0; i < pressableNotes.Length; i++)
        {
            // Every note is at a different height, no empty gaps because that is Submission Mode!
            _noteXLocation = currentMode == RelicSongMode.Aria ? ariaModeNoteLocationX[_pressableNoteXIndex[i] - 1] : pirouetteModeNoteLocationX[_pressableNoteXIndex[i] - 1];
            _noteLocation = new Vector3(_noteXLocation, 0.02f, noteHeightsY[_pressableNoteHeight[i] - 1]);

            // Set location and show
            pressableNotes[i].transform.localPosition = _noteLocation;
            pressableNotes[i].gameObject.SetActive(true);
        }
    }




    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //    Button Pressing Functions
    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

    void ClefGetsPressed()
    {
        // Don't do anything until Submission Mode is enabled
        // Don't even strike
        if (!isInSubmitMode) { return; }
        if (moduleSolved) { return; }


        Debug.LogFormat("[Relic Song #{0}] Pressed Clef.", moduleId);

        // Each individual note checks if it should be pressed or not.
        // Once we submit, we can just check if the count is good or not
        // If the count is good, we know it's valid since every note has validated itself first


        // First submit is Green Mode
        if (!hasSubmittedAriaAlready)
        {
            if (currentlySubmittedNotes.Count == currentValidAriaNotes.Count)
            {
                clef.AddInteractionPunch(1f);
                SwitchSubmissionModeToOrange();
            }
            else
            {
                Debug.LogFormat("[Relic Song #{0}] !!STRIKE!!  Not all correct Notes were submitted in Green Mode.  !!STRIKE!!", moduleId);
                thisModule.HandleStrike();

                // Smaller interaction Punch if incorrect, because the Clef doesn't move
                clef.AddInteractionPunch(0.3f);
            }
        }
        // Second submit is Orange Mode
        else
        {
            if (currentlySubmittedNotes.Count == currentValidPirouetteNotes.Count)
            {
                clef.AddInteractionPunch(1f);
                ModuleGetsSolved();
            }
            else
            {
                Debug.LogFormat("[Relic Song #{0}] !!STRIKE!!  Not all correct Notes were submitted in Orange Mode.  !!STRIKE!!", moduleId);
                thisModule.HandleStrike();

                // Smaller interaction Punch if incorrect, because the Clef doesn't move
                clef.AddInteractionPunch(0.3f);
            }
        }

    }


    void EmptyNoteGetsPressed(int noteID, int noteHeight, KMSelectable buttonReference)
    {
        // Shouldn't be able to press them before Submission mode, but just in case
        if (!isInSubmitMode) { return; }
        if (moduleSolved) { return; }


        // Can't submit the same Note Twice
        if (currentlySubmittedNotes.Contains(noteHeight))
        { return; }

        buttonReference.AddInteractionPunch(0.3f);
        // Play SFX
        thisModuleAudio.PlaySoundAtTransform(noteSounds[noteHeight - 1].name, transform);

        // Verify Green Mode for the first Submit
        if (!hasSubmittedAriaAlready)
        {
            // If the Note is part of the solution
            if (currentValidAriaNotes.Contains(noteHeight))
            {
                Debug.LogFormat("[Relic Song #{0}] Note Height {1} was expected in Green Mode. That is correct.", moduleId, noteHeight);
                // Add it to currently submitted notes
                currentlySubmittedNotes.Add(noteHeight);
                // Reveal the Note
                wholeNotes[noteID].gameObject.SetActive(true);
                wholeNotes[noteID].transform.localPosition = buttonReference.transform.localPosition;
            }
            else
            {
                Debug.LogFormat("[Relic Song #{0}] !!STRIKE!! Note Height {1} was not expected in Green Mode.  !!STRIKE!!", moduleId, noteHeight);
                // Not present? Strike
                thisModule.HandleStrike();
            }
        }
        // Verify Orange Mode for the second Submit
        else
        {
            // If the Note is part of the solution
            if (currentValidPirouetteNotes.Contains(noteHeight))
            {
                Debug.LogFormat("[Relic Song #{0}] Note Height {1} was expected in Orange Mode. That is correct.", moduleId, noteHeight);
                // Add it to currently submitted notes
                currentlySubmittedNotes.Add(noteHeight);
                // Reveal the Note
                wholeNotes[noteID].gameObject.SetActive(true);
                wholeNotes[noteID].transform.localPosition = buttonReference.transform.localPosition;
            }
            else
            {
                Debug.LogFormat("[Relic Song #{0}] !!STRIKE!! Note Height {1} was not expected in Orange Mode.  !!STRIKE!!", moduleId, noteHeight);
                // Not present? Strike
                thisModule.HandleStrike();
            }
        }

    }

    void SwitchSubmissionModeToOrange()
    {
        Debug.LogFormat("[Relic Song #{0}] All correct Notes were submitted in Green Mode. Switching to Orange Mode and awaiting for submission.", moduleId);

        hasSubmittedAriaAlready = true;


        // Switch to Orange Mode
        currentMode = RelicSongMode.Pirouette;
        musicSheetCubeRenderer.material = pirouetteMaterial;
        StartCoroutine(MoveClefToOtherSide());


        // Remove submitted Notes
        foreach (var _note in wholeNotes)
        {
            _note.gameObject.SetActive(false);
        }

        PlaceEmptyNotesOnSheetMusic();
        currentlySubmittedNotes.Clear();
    }

    void ModuleGetsSolved()
    {
        Debug.LogFormat("[Relic Song #{0}] All correct Notes were pressed and submitted. Solving Module.", moduleId);
        moduleSolved = true;

        // Switch back to Green Mode
        currentMode = RelicSongMode.Aria;

        StartCoroutine(MoveClefToOtherSide());

        StartCoroutine(AnimateModuleSolve());
        


        // Remove all visual Notes
        foreach (var _note in wholeNotes)
        {
            _note.gameObject.SetActive(false);
        }
        foreach (var _note in pressableNotes)
        {
            _note.gameObject.SetActive(false);
        }
    }

    IEnumerator AnimateModuleSolve()
    {
        // Wait for a single frame to allow the Clef to start moving
        // Doing two Coroutines at once seems to not make Unity happy
        yield return null;

        // Cycle through both materials a few times
        for (int i = 0; i < 6; i ++)
        {
            musicSheetCubeRenderer.material = ariaMaterial;

            yield return new WaitForSeconds(0.1f);

            musicSheetCubeRenderer.material = pirouetteMaterial;

            yield return new WaitForSeconds(0.1f);
        }


        // Custom Literally Shiny material
        musicSheetCubeRenderer.material = shinyMaterial;

        // Solve module sound & feedback
        thisModuleAudio.PlaySoundAtTransform(solveSound.name, transform);
        thisModule.HandlePass();
    }


    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //    Boss Module Setup
    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

    void GetModuleListData()
    {
        ignoredModulesList = thisBossModule.GetIgnoredModules(thisModule, new string[]{
                "14",
                "42",
                "501",
                "A>N<D",
                "Bamboozling Time Keeper",
                "Black Arrows",
                "Brainf---",
                "Busy Beaver",
                "Don't Touch Anything",
                "Floor Lights",
                "Forget Any Color",
                "Forget Enigma",
                "Forget Everything",
                "Forget Infinity",
                "Forget It Not",
                "Forget Maze Not",
                "Forget Me Later",
                "Forget Me Not",
                "Forget Perspective",
                "Forget The Colors",
                "Forget Them All",
                "Forget This",
                "Forget Us Not",
                "Iconic",
                "Keypad Directionality",
                "Kugelblitz",
                "Multitask",
                "OmegaDestroyer",
                "OmegaForest",
                "Organization",
                "Password Destroyer",
                "Purgatory",
                "Relic Song",
                "RPS Judging",
                "Security Council",
                "Shoddy Chess",
                "Simon Forgets",
                "Simon's Stages",
                "Souvenir",
                "Tallordered Keys",
                "The Time Keeper",
                "Timing is Everything",
                "The Troll",
                "Turn The Key",
                "The Twin",
                "Übermodule",
                "Ultimate Custom Night",
                "The Very Annoying Button",
                "Whiteout"
            });
        Debug.Log(ignoredModulesList.Length);


        // Get number of solvable modules not in the Ignored Modules list.
        // This is the number of Stages we will have available
        // Credit to Blananas2 in Iconic script
        totalStageCount = thisBombInfo.GetSolvableModuleNames().Where(a => !ignoredModulesList.Contains(a)).ToList().Count;
        Debug.Log(thisBombInfo.GetModuleNames().Count);

        Debug.LogFormat("[Relic Song #{0}] Found a total of {1} non-ignored modules, AKA total possible stages.", moduleId, totalStageCount);

    }


    IEnumerator CheckForSolves()
    {
        // Code from DuckKonundrum module
        // Loop
        while (!moduleSolved)
        {
            if (ignoredModulesList != null)
            {
                // Get current number of Solves
                currentNumberOfSolves = thisBombInfo.GetSolvedModuleNames().Where(a => !ignoredModulesList.Contains(a)).ToList().Count;
                // Compare with last known stage number
                if (currentNumberOfSolves > currentStageNumber)
                {
                    NewStageHappens();
                }
            }

            yield return new WaitForSeconds(.1f);
        }
    }




    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //    Twitch Plays
    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=


#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"“!{0} 2 3 5” to press the note heights 2 3 5 (bottommost is 1). “!{0} Switch” or “!{0} s” to Switch Mode. “!{0} 1 2 4 s 3 5 s” works too. Don't forget Spaces between numbers.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        // Credit to Royal_Flu$h for this line 
        var commandParts = command.ToLowerInvariant().Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        // Don't handle empty commands of course
        if (commandParts.Length == 0) {  yield break; }


        List<KMSelectable> submissionResults = new List<KMSelectable>();


        foreach (string _individualCommand in commandParts)
        {
            switch(_individualCommand)
            {
                case "switch":
                    clef.OnInteract();
                    yield return new WaitForSeconds(0.5f);
                    break;

                case "s":
                    clef.OnInteract();
                    yield return new WaitForSeconds(0.5f);
                    break;


                case "1":
                    pressableNotes[Array.IndexOf(_pressableNoteHeight, 1)].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                    break;


                case "2":
                    pressableNotes[Array.IndexOf(_pressableNoteHeight, 2)].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                    break;


                case "3":
                    pressableNotes[Array.IndexOf(_pressableNoteHeight, 3)].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                    break;


                case "4":
                    pressableNotes[Array.IndexOf(_pressableNoteHeight, 4)].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                    break;


                case "5":
                    pressableNotes[Array.IndexOf(_pressableNoteHeight, 5)].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                    break;

                case "0":
                    yield return "sendtochat {0} did you try to submit 0? Notes are indexed 1-5 with 1 being the bottommost and 5 the topmost!";
                    break;

                default:
                    break;
            }
        }

        yield break;
    }

    // Auto-solve if Twitch Plays needs to force a solve
    IEnumerator TwitchHandleForcedSolve()
    {
        Debug.LogFormat("[Relic Song #{0}] Received Force Solve TP command, waiting for Submit Mode to activate.", moduleId);
        // Boss Modules usually don't short-circuit themselves
        // See 14, Duck Konundrum, Forget Me Not
        while (!isInSubmitMode) { yield return true; }

        string currentModeValidNotes = " ";
        string otherModeValidNotes = " ";

        // We always start Submission mode in Aria mode
        if (currentValidAriaNotes.Count > 0)
        {
            foreach (int _note in currentValidAriaNotes)
            {
                currentModeValidNotes += _note + " ";
            }
        }
        if (currentValidPirouetteNotes.Count > 0)
        {
            foreach (int _note in currentValidPirouetteNotes)
            {
                otherModeValidNotes += _note + " ";
            }
        }

        Debug.LogFormat("[Relic Song #{0}] Force Solved via TP Solve command", moduleId);

        yield return ProcessTwitchCommand(currentModeValidNotes + " s " + otherModeValidNotes + " s");
    }

}
