
echo "This should be executed in PmxEditor's folder with PmxEditorPreviewGen.dll installed in its plugins folder"
# Example: . run_as_script.sh "folder_to_create_previews"
dotnet run --project "PmxPreviewRunner" "$1" |& tee PmxPreviewRunner.log
