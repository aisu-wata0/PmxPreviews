
# PmxPreviews

Generate preview images for .pmx or .pmd files, using PmxEditor.

# How to use

You will need a PmxEditor folder with a `PmxEditor_x64.exe` inside. If you don't use a clean installation the existing plugins might cause problems (confirmed).

Download from the releases:

* `PmxPreviewRunner.exe` and put it in the clean `PmxEditor` you will use.
* `PmxEditorPreviewGen.dll` which is the plugin file you should install (just put the file in `PmxEditor/_plugin/User/`)

Drag the folder you'd like to generate the previews into the executable `PmxPreviewRunner.exe` you just put in the `PmxEditor` folder. If you know what a command line is, you can alternatively run `PmxPreviewRunner.exe "folder_to_create_previews"` (in the `PmxEditor` folder of course).

## New preview .html version

Install `python` and these packages for it (search google on how to) `cv2`, `numpy`.

If you want google translations and need to also install `pykakasi` and `translators` and then run `translation_cache_generator/translate_all_file_tree_mmd.py`. This will generate a translation cache for the folder tree.

```bash
python preview_html.py "path_to_folder_tree_with_previews"
```


## Old preview .html version

Drag the folder into `PmxReportGen.exe` found in the project's releases.

# Compile

First of all, get the dependencies for `PmxEditorPreviewGen`, which are `slimdx` and `PEPlugin`. Actually read read `PmxEditorPreviewGen/PmxEditorPreviewGen.csproj`, there's a comment there about it depending on how you do it.

`compile.sh` contains the commands to build each component. This will generate most importantly:

* `PmxPreviewRunner.exe` somewhere inside the directory `PmxPreviewRunner/`
* `PmxEditorPreviewGen.dll` in `PmxEditorPreviewGen/`  which is the plugin file you should install (just put the file in `PmxEditor/_plugin/User/`)

