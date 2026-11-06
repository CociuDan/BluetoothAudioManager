# Chooses the version for a build, used by both the Release and the Build workflows.
# With a version asked for, such as 2.0.0, it's that one. Without, it's the highest tagged version with 1 added to its
# last number, or 0.0.1 for the first build. A version that's already tagged is refused, as each can be used only once.
# Needs the repository's tags, so check out with its whole history.
param(
    [string] $Requested = ""
)

$requested = $Requested.Trim().TrimStart('v', 'V')
$tagged = git tag --list 'v*' |
    ForEach-Object { $_.Substring(1) } |
    Where-Object { $_ -match '^\d+\.\d+\.\d+$' } |
    ForEach-Object { [version]$_ } |
    Sort-Object

if ($requested) {
    if ($requested -notmatch '^\d+\.\d+\.\d+$') {
        throw "The version must be three numbers, such as 2.0.0, not '$requested'."
    }
    $version = $requested
}
elseif ($tagged) {
    $last = @($tagged)[-1]
    $version = "$($last.Major).$($last.Minor).$($last.Build + 1)"
}
else {
    $version = '0.0.1'
}

if (git tag --list "v$version") {
    throw "Version $version was already built. Enter a higher one, or leave it empty."
}

return $version
