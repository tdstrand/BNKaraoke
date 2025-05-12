# Define the root folder
$rootFolder = "C:\users\tstra\source\repos\Bnkaraoke\BnKaraoke.DJ"

# Define the subfolders to process
$subFolders = @("Converters", "Models", "ViewModels", "Views")

# Define specific files to include from the root folder
$rootFiles = @("App.axaml", "program.cs", "BNKaraoke.DJ.csproj")

# Generate output file name with date and time
$dateTime = Get-Date -Format "yyyyMMdd_HHmmss"
$outputFile = Join-Path $rootFolder "Current_Programs_$dateTime.txt"

# Initialize output content
$outputContent = @()

# Process specific files in the root folder
foreach ($file in $rootFiles) {
    $filePath = Join-Path $rootFolder $file
    if (Test-Path $filePath) {
        $fileContent = Get-Content $filePath -Raw
        $outputContent += "Filename: $filePath"
        $outputContent += "=" * 10
        $outputContent += $fileContent
        $outputContent += "=" * 10
        $outputContent += ""
    }
}

# Process files in specified subfolders
foreach ($subFolder in $subFolders) {
    $folderPath = Join-Path $rootFolder $subFolder
    if (Test-Path $folderPath) {
        $files = Get-ChildItem -Path $folderPath -File
        foreach ($file in $files) {
            $filePath = $file.FullName
            $fileContent = Get-Content $filePath -Raw
            $outputContent += "Filename: $filePath"
            $outputContent += "=" * 10
            $outputContent += $fileContent
            $outputContent += "=" * 10
            $outputContent += ""
        }
    }
}

# Write to output file
$outputContent | Out-File -FilePath $outputFile -Encoding UTF8