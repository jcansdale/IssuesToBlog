workflow "Turn issues into blog posts" {
  resolves = ["IssuesToBlog"]
  on = "issues"
}

action "IssuesToBlog" {
  uses = "jcansdale/IssuesToBlog@master"
  args = "push"
  secrets = ["PERSONAL_ACCESS_TOKEN"]
}
