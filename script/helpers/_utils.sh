error() {
    echo "$@" >&2
}

fatal() {
    error "$@"
    exit 1
}

gh_group() {
    echo "::group::$@"
}

gh_endgroup() {
    echo "::endgroup::"
}

clean_branch_name() {
    local branch=$1
    echo $branch | sed 's@/@.@g'
}