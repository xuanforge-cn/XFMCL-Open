/**
 * app/build.gradle.kts - Android 模块
 * 玹砺科技 XUANFORGE
 *
 * 依赖最小化：Compose + Activity + 协程。
 * 网络使用 java.net.HttpURLConnection，JSON 使用 Android 内置 org.json，不引入额外解析库。
 * 本项目以 MIT 协议开源。
 */
import java.util.Properties

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}

/**
 * 签名凭据通过 signing.properties 指向外部 keystore.properties，
 * 凭据文件不存在时自动跳过 release 签名，构建不会失败。
 */
val credentialsFile: String? = run {
    val f = rootProject.file("signing.properties")
    if (!f.exists()) return@run null
    // 路径含中文，必须按 UTF-8 读取，否则 Properties 默认 ISO-8859-1 会乱码
    Properties().apply { f.reader(Charsets.UTF_8).use { load(it) } }.getProperty("credentialsFile")
}

val keystoreProperties: Properties? = credentialsFile?.let { path ->
    val f = file(path)
    if (!f.exists()) null else Properties().apply { f.reader(Charsets.UTF_8).use { load(it) } }
}

/**
 * storeFile 的 Windows 路径带单反斜杠，Properties 会把它当转义符吃掉，
 * 因此单独按原始文本读取这一行。
 */
val storeFilePath: String? = credentialsFile?.let { path ->
    val f = file(path)
    if (!f.exists()) null else f.readLines(Charsets.UTF_8)
        .firstOrNull { it.startsWith("storeFile=") }
        ?.substringAfter('=')
        ?.trim()
}

android {
    namespace = "cn.xuanforge.xfmclopen"
    compileSdk = 36

    defaultConfig {
        applicationId = "cn.xuanforge.xfmclopen"
        minSdk = 26
        targetSdk = 34
        versionCode = 1
        versionName = "1.0.0"
        resValue("string", "app_copyright", "© 2026 玹砺科技 XUANFORGE")
    }

    signingConfigs {
        if (keystoreProperties != null && storeFilePath != null) {
            create("release") {
                storeFile = file(storeFilePath)
                storePassword = keystoreProperties.getProperty("storePassword")
                keyAlias = keystoreProperties.getProperty("keyAlias")
                keyPassword = keystoreProperties.getProperty("keyPassword")
                enableV1Signing = false
                enableV2Signing = true
                enableV3Signing = true
                enableV4Signing = true
            }
        }
    }

    buildTypes {
        debug {
            isMinifyEnabled = false
            applicationIdSuffix = ".debug"
            versionNameSuffix = "-debug"
        }
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
            signingConfig = signingConfigs.findByName("release")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
        freeCompilerArgs += listOf(
            "-Xjvm-default=all",
            "-opt-in=kotlin.RequiresOptIn"
        )
    }

    buildFeatures {
        compose = true
    }

    packaging {
        resources {
            excludes += setOf(
                "/META-INF/{AL2.0,LGPL2.1}",
                "/META-INF/DEPENDENCIES",
                "/META-INF/LICENSE*",
                "/META-INF/NOTICE*",
                "META-INF/*.kotlin_module"
            )
        }
    }

    lint {
        abortOnError = false
        checkReleaseBuilds = false
    }
}

dependencies {
    // AndroidX 与 Compose（版本锁定，禁止浮动）
    implementation("androidx.activity:activity-compose:1.9.3")
    implementation(platform("androidx.compose:compose-bom:2024.10.00"))
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.foundation:foundation")
    implementation("androidx.compose.material3:material3")

    // 协程：下载与进度更新
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.9.0")
}
