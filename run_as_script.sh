echo "This should be executed in PmxEditor's folder with PmxEditorPreviewGen.dll installed in its plugins folder"
# Example:
# You have `PmxEditor/`` and this project's folder, `PmxPreviews/``, beside each other.
# copy this file to PmxEditor's folder
# open a terminal in that PmxEditor's folder
# run: . run_as_script.sh "M:\MMD\data\models\characters"
dotnet run --project "../PmxPreviews/PmxPreviewRunner" "$1" |& tee PmxPreviewRunner.log
