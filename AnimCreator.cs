﻿#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Text;
using System.IO;
using System;

public class AnimCreator : EditorWindow
{
    GameObject _parent;
    GameObject _animated;

    AnimatorController _exisitngAnimator;

    int _animationsIndex = 0, _previousAnimationsIndex = 0;
    bool _foldoutStatus = true, _defaultAnimationType = false, _useExistingAnimator = false;

    string _path = "Assets/Main/Generated Animations/";

    GameObject[] animationComponents;

    struct AnimationPair { public AnimationMetadata On; public AnimationMetadata Off; }
    struct AnimationMetadata { public string name; public AnimationClip clip; }
    List<AnimationPair> _animationPairList = new List<AnimationPair>();


    [MenuItem("Tools/Arsenicu/AnimCreator")]
    static void Init()
    {
        AnimCreator window = (AnimCreator)EditorWindow.GetWindow(typeof(AnimCreator));
        window.Show();
    }

    void OnGUI()
    {
        //haha lol lmao xd
        #pragma warning disable CS0618
        #pragma warning disable CS0168

        GUILayout.Label("Parent is the GameObject with the animator.");
        
        //we only ever need one parent for these animations
        _parent = (GameObject)EditorGUILayout.ObjectField("Parent: ", _parent, typeof(GameObject));

        if(GUILayout.Button("Open folder dialog"))
        {
            _path = EditorUtility.OpenFolderPanel("Pick destination folder", "", "");

            //so we need to convert the full path with driveletter into unity-relative terms
            string[] split = _path.Split('/');
            StringBuilder sb = new StringBuilder();
            //haha this is cancer but its how i know how to do things
            bool rootFound = false;
            for(int i = 0; i < split.Length; i++)
            {
                if(split[i] == "Assets")
                    rootFound = true;
                if (rootFound)
                {
                    sb.Append(split[i]);
                    sb.Append("/");
                }
            }

            //dont be a troll and select some location outside of the assetfolder
            if(!rootFound)
                throw new Exception();
            _path = sb.ToString();
        }

        //check if the user wants to use their own custom animator
        _useExistingAnimator = EditorGUILayout.Toggle("Use Existing Animator?", _useExistingAnimator);
        if(_useExistingAnimator)
            _exisitngAnimator = (AnimatorController)EditorGUILayout.ObjectField("Animator: ", _exisitngAnimator, typeof(AnimatorController));

        //amount of animated children
        _animationsIndex = EditorGUILayout.IntField("Amount:", _animationsIndex);

        //check if the index changed since last frame
        if(_animationsIndex != _previousAnimationsIndex)
        {
            //there is a change from the last frame. we have to mutate the array - 

            //quick n dirty. make a temporray with the new size
            GameObject[] tempArray = new GameObject[_animationsIndex];

            //if its null we dont want to copy anything
            if(animationComponents != null)
            {
                for(int i = 0; i < Math.Min(animationComponents.Length, _animationsIndex) ; i++)
                {//copy everything over from the old one without writing to it
                    try
                    {
                        tempArray[i] = animationComponents[i];
                    }
                    catch(IndexOutOfRangeException e)
                    {
                        break;
                    }
                }
            }

            //assign the new one as the old one so we dont have to rename anything
            animationComponents = tempArray;
            //dont forget to update _previousAnimationsIndex!
            _previousAnimationsIndex = _animationsIndex;
        }

        //logic for toggling the foldout
        _foldoutStatus = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutStatus, "Stuff");

        if(_foldoutStatus)
        {
            for(int i = 0; i < _animationsIndex; i++)
            {
                animationComponents[i] = (GameObject)EditorGUILayout.ObjectField("Object to animate: ", animationComponents[i], typeof(GameObject));
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();

        _defaultAnimationType = EditorGUILayout.Toggle("Default animation On?", _defaultAnimationType);
        
        if(GUILayout.Button("Animate!"))
        {
            _animationPairList.Clear();
            for(int i = 0; i < _animationsIndex; i++)
            {
                Animate(animationComponents[i]);
            }
            CreateAnimator();
        }
    }

    void Animate(GameObject _animated)
    {

        //ensure both are filled in
        if(!_parent || !_animated)
            return;

        //check if _animated is a child of _parent
        Transform[] bruh = _parent.GetComponentsInChildren<Transform>();

        bool childFound = false;

        foreach(Transform child in bruh)
            if(child == _animated.transform)
            {
                childFound = true;
            }

        //leave if if its not
        if(!childFound)
        {
            Debug.Log("Child Not found!");
            return;
        }

        //create a relative hierarchy path
        GameObject currentGameObject = _animated;
        List<string> stringList = new List<string>();

        while(currentGameObject != _parent)
        {
            stringList.Add(currentGameObject.name);
            currentGameObject = currentGameObject.transform.parent.gameObject;
        }

        string[] hierarchyArray = stringList.ToArray();
        StringBuilder sb = new StringBuilder();

        //reverse it for correct formatting
        for(int i = hierarchyArray.Length - 1; i >= 0; i--)
        {
            sb.Append(hierarchyArray[i]);
            if(i > 0)
            sb.Append("/");
        }

        AnimationClip onAnim = new AnimationClip();
        onAnim.SetCurve(sb.ToString(), typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0, 0, 1));

        AnimationClip offAnim = new AnimationClip();
        offAnim.SetCurve(sb.ToString(), typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0, 0, 0));

        //check if directory exists
        if(!AssetDatabase.IsValidFolder(_path))
            Directory.CreateDirectory(_path);

        //overwrite previous assets with the same name - potentially dangerous, but this is my preferred default behavior
        EFile.DeleteIfExists(_path + _animated.name + " On.anim");
        EFile.DeleteIfExists(_path + _animated.name + " Off.anim");

        AssetDatabase.CreateAsset(onAnim, _path + _animated.name + " On.anim");
        AssetDatabase.CreateAsset(offAnim, _path + _animated.name + " Off.anim");

        //this is a yikes but im lazy
        AnimationPair temp;
        AnimationMetadata on;
        AnimationMetadata off;
        on.name = _animated.name;
        off.name = _animated.name;
        on.clip = onAnim;
        off.clip = offAnim;
        temp.On = on;
        temp.Off = off;
        _animationPairList.Add(temp);

        //Debug.Log($"Created animations: '{onAnim.ToString()}' and '{offAnim.ToString()}'");
    }

    void CreateAnimator()
    {
        AnimatorController controller;
        //check if there's a loaded animator - if yes, we want to use the loaded one
        if(_exisitngAnimator == null)
        {
            EFile.DeleteIfExists(_path + "generated controller.controller");
            controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(_path + "generated controller.controller");
        }
        else
        {
            controller = _exisitngAnimator;
        }

        //This will actually load in the animator if it already exists. We need to make sure it doesnt do that, because Im lazy and this is MY tool.

        //phases: 
        //1. add bool parameters for each animation pair
        //2. add layer for each pair
        //3. set up statemachines & transitions for each pair

        AnimationPair[] pairs = _animationPairList.ToArray();
        
        //check if the animator is new or not - used for accounting for pre-exisitng layer indexes
        int offset = 1;
        if(controller.layers.Length > 1)
            offset = controller.layers.Length;

        for(int i = 0; i < pairs.Length; i++)
        {
            //make sure that the name is unique
            string uniquename = controller.MakeUniqueParameterName(pairs[i].On.name);
            //and that we use the unique name from now on 
            pairs[i].On.name = uniquename;
            pairs[i].Off.name = uniquename;

            controller.AddParameter(uniquename, AnimatorControllerParameterType.Bool);

            //we need to instantiate the layer like this to ensure that the settings are set correctly.
            AnimatorControllerLayer templayer = new AnimatorControllerLayer
            {
                name = pairs[i].On.name,
                defaultWeight = 1.0f,
                stateMachine = new AnimatorStateMachine(),
            };
            controller.AddLayer(templayer);
        }



        for(int i = offset; i < controller.layers.Length; i++)
        {//i = 1 to skip base layer

            //create the states
            AnimatorState stateOn = new AnimatorState { motion = pairs[i - offset].On.clip, name = pairs[i - offset].On.name + " On" };
            AnimatorState stateOff = new AnimatorState { motion = pairs[i - offset].Off.clip, name = pairs[i - offset].Off.name + " Off" };

            //create the animator state transitions
            AnimatorStateTransition transitionOn = new AnimatorStateTransition { destinationState = stateOn, conditions = new AnimatorCondition[] { new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = pairs[i - offset].On.name}}};
            AnimatorStateTransition transitionOff = new AnimatorStateTransition { destinationState = stateOff, conditions = new AnimatorCondition[] { new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = pairs[i - offset].Off.name}}};

            //add the new transitiots to the 'anystate' node
            controller.layers[i].stateMachine.anyStateTransitions = new AnimatorStateTransition[] { transitionOn, transitionOff };

            //default types - for when you want something off by default, or on by default!
            if(_defaultAnimationType)
            {
                controller.layers[i].stateMachine.AddState(stateOn, new Vector3(300, 110, 0));
                controller.layers[i].stateMachine.AddState(stateOff, new Vector3(300, 30, 0));
            }
            else
            {
                controller.layers[i].stateMachine.AddState(stateOff, new Vector3(300, 110, 0));
                controller.layers[i].stateMachine.AddState(stateOn, new Vector3(300, 30, 0));
            }

            stateOn.hideFlags = HideFlags.HideInHierarchy;
            stateOff.hideFlags = HideFlags.HideInHierarchy;
            transitionOn.hideFlags = HideFlags.HideInHierarchy;
            transitionOff.hideFlags = HideFlags.HideInHierarchy;
            controller.layers[i].stateMachine.hideFlags = HideFlags.HideInHierarchy;


            AssetDatabase.AddObjectToAsset(transitionOn, controller);
            AssetDatabase.AddObjectToAsset(transitionOff, controller);

            AssetDatabase.AddObjectToAsset(stateOn, controller);
            AssetDatabase.AddObjectToAsset(stateOff, controller);

            AssetDatabase.AddObjectToAsset(controller.layers[i].stateMachine, controller);

            //not sure if this is neccesary but here it is
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(controller));

        }



    }

}

//well its a right shame C# doesnt support static extension methods. This'd come in right handy...
public static class EFile
{
    public static bool DeleteIfExists(string path)
    {
        if(File.Exists(path))
        {
            File.Delete(path);
            return true;
        }
        return false;
    }
}
#endif