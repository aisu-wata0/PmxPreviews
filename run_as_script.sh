
echo "This should be executed in PmxEditor's folder with PmxEditorPreviewGen.dll installed in its plugins folder"
# Example:
# copy this file to PmxEditor's folder
# open a terminal in that PmxEditor's folder
# run: . run_as_script.sh "M:\MMD\data\models\characters"
dotnet run --project "../PmxPreviews/PmxPreviewRunner" "$1" |& tee PmxPreviewRunner.log
