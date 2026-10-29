/**
 * settings.gradle.kts - Gradle 设置
 * 玹砺科技 XUANFORGE
 *
 * 本项目以 MIT 协议开源。
 */

pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}

rootProject.name = "XFMCL Open Android"
include(":app")
